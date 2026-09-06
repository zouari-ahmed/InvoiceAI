using System.Globalization;
using InvoiceAI.Application;
using InvoiceAI.Application.DTOs;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceAI.Infrastructure.Processing;

public class InvoiceProcessor : IInvoiceProcessor
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IInvoiceExtractor _extractor;
    private readonly ILogger<InvoiceProcessor> _logger;

    public InvoiceProcessor(
        AppDbContext db,
        IFileStorage storage,
        IInvoiceExtractor extractor,
        ILogger<InvoiceProcessor> logger)
    {
        _db = db;
        _storage = storage;
        _extractor = extractor;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(int invoiceId, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .Include(i => i.SourceFile)
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice {InvoiceId} no longer exists. Skipping.", invoiceId);
            return false;
        }

        if (invoice.SourceFile is null)
        {
            _logger.LogWarning("Invoice {InvoiceId} has no source file.", invoiceId);
            invoice.Status = InvoiceStatus.ExtractionFailed;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        invoice.Status = InvoiceStatus.Processing;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        ExtractionResultDto result;

        try
        {
            await using var stream = await _storage.OpenAsync(invoice.SourceFile.StorageKey, ct);
            result = await _extractor.ExtractAsync(stream, invoice.SourceFile.ContentType, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure extracting invoice {InvoiceId}.", invoiceId);
            invoice.Status = InvoiceStatus.ExtractionFailed;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        invoice.RawExtractionJson = result.RawJson;

        if (!result.Success || result.Invoice is null)
        {
            _logger.LogWarning("Extraction failed for invoice {InvoiceId}: {Error}",
                invoiceId, result.ErrorMessage);

            invoice.Status = InvoiceStatus.ExtractionFailed;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        await ApplyExtractionAsync(invoice, result.Invoice, ct);

        invoice.Status = InvoiceStatus.PendingReview;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Invoice {InvoiceId} extracted: {Number}, total {Total} {Currency}.",
            invoiceId, invoice.InvoiceNumber, invoice.Total, invoice.Currency);

        return true;
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
}