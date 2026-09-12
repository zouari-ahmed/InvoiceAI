using System.Globalization;
using System.Text.Json;
using InvoiceAI.Application.DTOs;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> Get(CancellationToken ct)
    {
        var byStatus = await _db.Invoices
            .AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalInvoices = byStatus.Sum(s => s.Count);

        var totalAmount = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Reviewed
                        || i.Status == InvoiceStatus.Exported)
            .SumAsync(i => (decimal?)i.Total, ct) ?? 0m;
        var supplierRows = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.SupplierId != null)
            .Select(i => new { SupplierId = i.SupplierId!.Value, i.Total })
            .ToListAsync(ct);

        var supplierIds = supplierRows
            .Select(r => r.SupplierId)
            .Distinct()
            .ToList();

        var supplierNames = await _db.Parties
            .AsNoTracking()
            .Where(p => supplierIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var topSuppliers = supplierRows
            .GroupBy(r => r.SupplierId)
            .Select(g => new SupplierStatDto(
                supplierNames.TryGetValue(g.Key, out var name) ? name : "(unknown)",
                g.Count(),
                g.Sum(x => x.Total)))
            .OrderByDescending(s => s.Total)
            .Take(5)
            .ToList();

        var reviewed = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Where(i => i.RawExtractionJson != null
                        && (i.Status == InvoiceStatus.Reviewed
                            || i.Status == InvoiceStatus.Exported))
            .ToListAsync(ct);

        var compared = 0;
        var correct = 0;

        foreach (var invoice in reviewed)
        {
            var r = CompareExtraction(invoice);
            compared += r.Compared;
            correct += r.Correct;
        }

        double? accuracy = compared == 0
            ? null
            : Math.Round(correct * 100.0 / compared, 1);

        return Ok(new DashboardStatsDto(
            totalInvoices,
            totalAmount,
            "TND",
            byStatus.Select(s => new StatusCountDto(s.Status.ToString(), s.Count)).ToList(),
            topSuppliers,
            reviewed.Count,
            compared,
            correct,
            accuracy));
    }
    private static (int Compared, int Correct) CompareExtraction(Invoice invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.RawExtractionJson))
        {
            return (0, 0);
        }

        ExtractedInvoiceDto? raw;

        try
        {
            raw = JsonSerializer.Deserialize<ExtractedInvoiceDto>(
                invoice.RawExtractionJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return (0, 0);
        }

        if (raw is null)
        {
            return (0, 0);
        }

        var compared = 0;
        var correct = 0;

        void Check(bool proposed, bool matches)
        {
            if (!proposed) return;
            compared++;
            if (matches) correct++;
        }
        Check(raw.InvoiceNumber is not null,
            string.Equals(raw.InvoiceNumber?.Trim(), invoice.InvoiceNumber?.Trim(),
                StringComparison.OrdinalIgnoreCase));

        Check(raw.IssueDate is not null, ParseDate(raw.IssueDate) == invoice.IssueDate);
        Check(raw.DueDate is not null, ParseDate(raw.DueDate) == invoice.DueDate);

        Check(raw.Supplier?.Name is not null,
            string.Equals(raw.Supplier?.Name?.Trim(), invoice.Supplier?.Name?.Trim(),
                StringComparison.OrdinalIgnoreCase));

        Check(raw.Subtotal is not null, raw.Subtotal == invoice.Subtotal);
        Check(raw.VatAmount is not null, raw.VatAmount == invoice.VatAmount);
        Check(raw.Total is not null, raw.Total == invoice.Total);

        return (compared, correct);
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            return iso;
        }

        string[] fallbacks = ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "dd.MM.yyyy"];

        return DateOnly.TryParseExact(value, fallbacks,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback)
            ? fallback
            : null;
    }
}