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
    private readonly IPartyResolver _parties;

    public InvoicesController(
        AppDbContext db,
        IFileStorage storage,
        IExtractionQueue queue,
        IInvoiceProcessor processor,
        IPartyResolver parties)
    {
        _db = db;
        _storage = storage;
        _queue = queue;
        _processor = processor;
        _parties = parties;
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
    [HttpGet("{id:int}/details")]
    public async Task<ActionResult<InvoiceDetailDto>> GetDetails(int id, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Include(i => i.Customer)
            .Include(i => i.SourceFile)
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null)
        {
            return NotFound();
        }

        return Ok(MapToDetail(invoice));
    }

    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> GetFile(int id, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.SourceFile)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice?.SourceFile is null)
        {
            return NotFound();
        }

        Stream stream;

        try
        {
            stream = await _storage.OpenAsync(invoice.SourceFile.StorageKey, ct);
        }
        catch (FileNotFoundException)
        {
            return NotFound("The stored document is missing from disk.");
        }

        return File(
            stream,
            invoice.SourceFile.ContentType,
            invoice.SourceFile.OriginalFileName,
            enableRangeProcessing: true);
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

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> Update(
        int id,
        [FromBody] UpdateInvoiceDto dto,
        CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null)
        {
            return NotFound();
        }

        invoice.InvoiceNumber = string.IsNullOrWhiteSpace(dto.InvoiceNumber)
            ? null
            : dto.InvoiceNumber.Trim();

        invoice.IssueDate = dto.IssueDate;
        invoice.DueDate = dto.DueDate;
        invoice.Subtotal = dto.Subtotal;
        invoice.VatAmount = dto.VatAmount;
        invoice.Total = dto.Total;

        if (!string.IsNullOrWhiteSpace(dto.Currency))
        {
            invoice.Currency = dto.Currency.Trim().ToUpperInvariant();
        }

        invoice.SupplierId = await _parties.ResolveAsync(
            dto.Supplier?.Name, dto.Supplier?.Address, dto.Supplier?.TaxId, ct);

        invoice.CustomerId = await _parties.ResolveAsync(
            dto.Customer?.Name, dto.Customer?.Address, dto.Customer?.TaxId, ct);

        _db.InvoiceLineItems.RemoveRange(invoice.LineItems);
        invoice.LineItems.Clear();

        foreach (var line in dto.LineItems ?? [])
        {
            invoice.LineItems.Add(new InvoiceLineItem
            {
                Description = string.IsNullOrWhiteSpace(line.Description)
                    ? "(no description)"
                    : line.Description.Trim(),
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                VatRate = line.VatRate,
                LineTotal = line.LineTotal ?? line.Quantity * line.UnitPrice
            });
        }

        invoice.Status = InvoiceStatus.Reviewed;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var saved = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Include(i => i.Customer)
            .Include(i => i.SourceFile)
            .Include(i => i.LineItems)
            .FirstAsync(i => i.Id == id, ct);

        return Ok(MapToDetail(saved));
    }
    private static InvoiceSummaryDto MapToSummary(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.IssueDate,
        invoice.Supplier?.Name,
        invoice.Total,
        invoice.Currency,
        invoice.Status.ToString());

    private static InvoiceDetailDto MapToDetail(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.IssueDate,
        invoice.DueDate,
        invoice.Supplier is null
            ? null
            : new PartyDto(
                invoice.Supplier.Id,
                invoice.Supplier.Name,
                invoice.Supplier.Address,
                invoice.Supplier.TaxId),
        invoice.Customer is null
            ? null
            : new PartyDto(
                invoice.Customer.Id,
                invoice.Customer.Name,
                invoice.Customer.Address,
                invoice.Customer.TaxId),
        invoice.Subtotal,
        invoice.VatAmount,
        invoice.Total,
        invoice.Currency,
        invoice.Status.ToString(),
        invoice.SourceFile?.OriginalFileName,
        invoice.SourceFile?.ContentType,
        invoice.CreatedAt,
        invoice.UpdatedAt,
        invoice.LineItems
            .Select(l => new LineItemDto(
                l.Id,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                l.VatRate,
                l.LineTotal))
            .ToList());
}