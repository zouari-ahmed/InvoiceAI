using System.Security.Claims;
using InvoiceAI.Application;
using InvoiceAI.Application.DTOs;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private static readonly string[] AllowedContentTypes =
        ["application/pdf", "image/jpeg", "image/png"];

    private const long MaxFileBytes = 20 * 1024 * 1024;
    private const int MaxFilesPerRequest = 20;

    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IExtractionQueue _queue;
    private readonly IInvoiceProcessor _processor;

    public InvoicesController(
        AppDbContext db,
        IFileStorage storage,
        IExtractionQueue queue,
        IInvoiceProcessor processor)
    {
        _db = db;
        _storage = storage;
        _queue = queue;
        _processor = processor;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvoiceSummaryDto>>> GetAll(CancellationToken ct)
    {
        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return Ok(invoices.Select(MapToSummary));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceSummaryDto>> GetById(int id, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null)
        {
            return NotFound();
        }

        return Ok(MapToSummary(invoice));
    }
    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<UploadResultDto>> Upload(
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest("No files were sent.");
        }

        if (files.Count > MaxFilesPerRequest)
        {
            return BadRequest($"Too many files. The maximum is {MaxFilesPerRequest} per request.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        var accepted = new List<UploadedFileDto>();
        var rejected = new List<RejectedFileDto>();

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                rejected.Add(new RejectedFileDto(file.FileName, "The file is empty."));
                continue;
            }

            if (file.Length > MaxFileBytes)
            {
                rejected.Add(new RejectedFileDto(file.FileName, "The file is larger than 20 MB."));
                continue;
            }

            var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedContentTypes.Contains(contentType))
            {
                rejected.Add(new RejectedFileDto(
                    file.FileName,
                    $"Unsupported type '{file.ContentType}'. Allowed: PDF, JPEG, PNG."));
                continue;
            }

            string storageKey;
            await using (var stream = file.OpenReadStream())
            {
                storageKey = await _storage.SaveAsync(stream, file.FileName, ct);
            }

            var sourceFile = new SourceFile
            {
                StorageKey = storageKey,
                OriginalFileName = Path.GetFileName(file.FileName),
                ContentType = contentType,
                SizeBytes = file.Length,
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow
            };

            var invoice = new Invoice
            {
                SourceFile = sourceFile,
                Status = InvoiceStatus.Uploaded,
                Currency = "TND",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.SourceFiles.Add(sourceFile);
            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync(ct);

            await _queue.EnqueueAsync(invoice.Id, ct);

            accepted.Add(new UploadedFileDto(
                invoice.Id,
                sourceFile.Id,
                sourceFile.OriginalFileName,
                sourceFile.SizeBytes));
        }

        return Accepted(new UploadResultDto(accepted, rejected));
    }
    [HttpPost("{id:int}/extract")]
    public async Task<ActionResult<InvoiceSummaryDto>> Extract(int id, CancellationToken ct)
    {
        var exists = await _db.Invoices.AnyAsync(i => i.Id == id, ct);

        if (!exists)
        {
            return NotFound();
        }

        var success = await _processor.ProcessAsync(id, ct);

        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .FirstAsync(i => i.Id == id, ct);

        if (!success)
        {
            return StatusCode(502, new
            {
                invoiceId = invoice.Id,
                status = invoice.Status.ToString(),
                error = "Extraction failed. See server logs for details."
            });
        }

        return Ok(MapToSummary(invoice));
    }

    private static InvoiceSummaryDto MapToSummary(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.IssueDate,
        invoice.Supplier?.Name,
        invoice.Total,
        invoice.Currency,
        invoice.Status.ToString());
}