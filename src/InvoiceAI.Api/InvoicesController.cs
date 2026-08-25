using InvoiceAI.Application.DTOs;
using InvoiceAI.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    // Temporary in-memory data. Replaced by the database in Phase 2.
    private static readonly List<Invoice> _invoices =
    [
        new Invoice
        {
            Id = 1,
            InvoiceNumber = "FA-2026-0142",
            IssueDate = new DateOnly(2026, 8, 12),
            DueDate = new DateOnly(2026, 9, 11),
            SupplierName = "SOTUMETAL SARL",
            SupplierTaxId = "1234567/A/M/000",
            Subtotal = 1250.000m,
            VatAmount = 237.500m,
            Total = 1487.500m,
            Status = InvoiceStatus.PendingReview
        },
        new Invoice
        {
            Id = 2,
            InvoiceNumber = "FA-2026-0143",
            IssueDate = new DateOnly(2026, 8, 18),
            SupplierName = "Tunisie Telecom",
            Subtotal = 89.000m,
            VatAmount = 16.910m,
            Total = 105.910m,
            Status = InvoiceStatus.Reviewed
        }
    ];

    [HttpGet]
    public ActionResult<IEnumerable<InvoiceSummaryDto>> GetAll()
    {
        var summaries = _invoices.Select(MapToSummary);
        return Ok(summaries);
    }

    [HttpGet("{id:int}")]
    public ActionResult<InvoiceSummaryDto> GetById(int id)
    {
        var invoice = _invoices.FirstOrDefault(i => i.Id == id);

        if (invoice is null)
            return NotFound(new { message = $"Invoice {id} not found." });

        return Ok(MapToSummary(invoice));
    }

    private static InvoiceSummaryDto MapToSummary(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.IssueDate,
        invoice.SupplierName,
        invoice.Total,
        invoice.Currency,
        invoice.Status.ToString()
    );
}                                                               