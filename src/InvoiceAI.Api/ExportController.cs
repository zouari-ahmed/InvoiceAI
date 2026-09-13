using ClosedXML.Excel;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _db;

    public ExportController(AppDbContext db)
    {
        _db = db;
    }

    private static readonly string[] Headers =
    [
        "Id", "InvoiceNumber", "IssueDate", "DueDate",
        "Supplier", "SupplierTaxId", "Customer", "CustomerTaxId",
        "Subtotal", "VatAmount", "Total", "Currency", "Status"
    ];

    [HttpGet]
    public async Task<IActionResult> Export(
        [FromQuery] string format = "csv",
        [FromQuery] bool all = false,
        CancellationToken ct = default)
    {
        var query = _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Include(i => i.Customer)
            .AsQueryable();

        if (!all)
        {
            query = query.Where(i => i.Status == InvoiceStatus.Reviewed
                                     || i.Status == InvoiceStatus.Exported);
        }

        var invoices = await query.OrderBy(i => i.Id).ToListAsync(ct);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");

        return format.ToLowerInvariant() switch
        {
            "json" => Ok(invoices.Select(ToRow)),
            "xlsx" => File(BuildXlsx(invoices),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"invoices-{stamp}.xlsx"),
            _ => File(BuildCsv(invoices), "text/csv", $"invoices-{stamp}.csv")
        };
    }

    private static object ToRow(Invoice i) => new
    {
        i.Id,
        i.InvoiceNumber,
        IssueDate = i.IssueDate?.ToString("yyyy-MM-dd"),
        DueDate = i.DueDate?.ToString("yyyy-MM-dd"),
        Supplier = i.Supplier?.Name,
        SupplierTaxId = i.Supplier?.TaxId,
        Customer = i.Customer?.Name,
        CustomerTaxId = i.Customer?.TaxId,
        i.Subtotal,
        i.VatAmount,
        i.Total,
        i.Currency,
        Status = i.Status.ToString()
    };
    private static byte[] BuildCsv(List<Invoice> invoices)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Headers));

        foreach (var i in invoices)
        {
            var cells = new[]
            {
                i.Id.ToString(CultureInfo.InvariantCulture),
                i.InvoiceNumber ?? "",
                i.IssueDate?.ToString("yyyy-MM-dd") ?? "",
                i.DueDate?.ToString("yyyy-MM-dd") ?? "",
                i.Supplier?.Name ?? "",
                i.Supplier?.TaxId ?? "",
                i.Customer?.Name ?? "",
                i.Customer?.TaxId ?? "",
                i.Subtotal.ToString("0.000", CultureInfo.InvariantCulture),
                i.VatAmount.ToString("0.000", CultureInfo.InvariantCulture),
                i.Total.ToString("0.000", CultureInfo.InvariantCulture),
                i.Currency,
                i.Status.ToString()
            };

            sb.AppendLine(string.Join(';', cells.Select(EscapeCsv)));
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            .GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
    private static byte[] BuildXlsx(List<Invoice> invoices)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Invoices");

        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = Headers[c];
        }

        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;

        foreach (var i in invoices)
        {
            sheet.Cell(row, 1).Value = i.Id;
            sheet.Cell(row, 2).Value = i.InvoiceNumber ?? "";
            sheet.Cell(row, 3).Value = i.IssueDate?.ToString("yyyy-MM-dd") ?? "";
            sheet.Cell(row, 4).Value = i.DueDate?.ToString("yyyy-MM-dd") ?? "";
            sheet.Cell(row, 5).Value = i.Supplier?.Name ?? "";
            sheet.Cell(row, 6).Value = i.Supplier?.TaxId ?? "";
            sheet.Cell(row, 7).Value = i.Customer?.Name ?? "";
            sheet.Cell(row, 8).Value = i.Customer?.TaxId ?? "";
            sheet.Cell(row, 9).Value = i.Subtotal;
            sheet.Cell(row, 10).Value = i.VatAmount;
            sheet.Cell(row, 11).Value = i.Total;
            sheet.Cell(row, 12).Value = i.Currency;
            sheet.Cell(row, 13).Value = i.Status.ToString();
            row++;
        }

        if (row > 2)
        {
            sheet.Range(2, 9, row - 1, 11).Style.NumberFormat.Format = "0.000";
        }

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}