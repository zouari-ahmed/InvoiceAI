using System.Globalization;
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
    private readonly IInvoiceExtractor _extractor;

    public InvoicesController(
        AppDbContext db,
        IFileStorage storage,
        IInvoiceExtractor extractor)
    {
        _db = db;
        _storage = storage;
        _extractor = extractor;
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

            accepted.Add(new UploadedFileDto(
                invoice.Id,
                sourceFile.Id,
                sourceFile.OriginalFileName,
                sourceFile.SizeBytes));
        }

        return Ok(new UploadResultDto(accepted, rejected));
    }
    [HttpPost("{id:int}/extract")]
    public async Task<ActionResult<InvoiceSummaryDto>> Extract(int id, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .Include(i => i.SourceFile)
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.SourceFile is null)
        {
            return BadRequest("This invoice has no source document to extract from.");
        }

        invoice.Status = InvoiceStatus.Processing;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        ExtractionResultDto result;
        await using (var stream = await _storage.OpenAsync(invoice.SourceFile.StorageKey, ct))
        {
            result = await _extractor.ExtractAsync(stream, invoice.SourceFile.ContentType, ct);
        }

        invoice.RawExtractionJson = result.RawJson;

        if (!result.Success || result.Invoice is null)
        {
            invoice.Status = InvoiceStatus.ExtractionFailed;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return StatusCode(502, new
            {
                invoiceId = invoice.Id,
                status = invoice.Status.ToString(),
                error = result.ErrorMessage
            });
        }

        await ApplyExtractionAsync(invoice, result.Invoice, ct);

        invoice.Status = InvoiceStatus.PendingReview;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _db.Entry(invoice).Reference(i => i.Supplier).LoadAsync(ct);

        return Ok(MapToSummary(invoice));
    }

    private async Task ApplyExtractionAsync(
        Invoice invoice,
        ExtractedInvoiceDto data,
        CancellationToken ct)
    {
        invoice.InvoiceNumber = data.InvoiceNumber;
        invoice.IssueDate = ParseDate(data.IssueDate);
        invoice.DueDate = ParseDate(data.DueDate);
        invoice.Subtotal = data.Subtotal ?? 0m;
        invoice.VatAmount = data.VatAmount ?? 0m;
        invoice.Total = data.Total ?? 0m;

        if (!string.IsNullOrWhiteSpace(data.Currency))
        {
            invoice.Currency = data.Currency.Trim().ToUpperInvariant();
        }

        invoice.SupplierId = (await ResolvePartyAsync(data.Supplier, ct))?.Id;
        invoice.CustomerId = (await ResolvePartyAsync(data.Customer, ct))?.Id;

        _db.InvoiceLineItems.RemoveRange(invoice.LineItems);
        invoice.LineItems.Clear();

        foreach (var line in data.LineItems ?? [])
        {
            invoice.LineItems.Add(new InvoiceLineItem
            {
                Description = string.IsNullOrWhiteSpace(line.Description)
                    ? "(no description)"
                    : line.Description.Trim(),
                Quantity = line.Quantity ?? 0m,
                UnitPrice = line.UnitPrice ?? 0m,
                VatRate = line.VatRate,
                LineTotal = line.LineTotal
                            ?? (line.Quantity ?? 0m) * (line.UnitPrice ?? 0m)
            });
        }
    }
    private async Task<Party?> ResolvePartyAsync(ExtractedPartyDto? dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
        {
            return null;
        }

        var name = dto.Name.Trim();
        var taxId = string.IsNullOrWhiteSpace(dto.TaxId) ? null : dto.TaxId.Trim();

        Party? existing = null;

        if (taxId is not null)
        {
            existing = await _db.Parties.FirstOrDefaultAsync(p => p.TaxId == taxId, ct);
        }

        existing ??= await _db.Parties.FirstOrDefaultAsync(p => p.Name == name, ct);

        if (existing is not null)
        {
            existing.TaxId ??= taxId;
            existing.Address ??= dto.Address?.Trim();
            return existing;
        }

        var created = new Party
        {
            Name = name,
            Address = dto.Address?.Trim(),
            TaxId = taxId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Parties.Add(created);
        await _db.SaveChangesAsync(ct);

        return created;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            return iso;
        }

        string[] fallbacks = ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "dd.MM.yyyy"];

        if (DateOnly.TryParseExact(value, fallbacks,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
        {
            return fallback;
        }

        return null;
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