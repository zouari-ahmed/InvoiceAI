using InvoiceAI.Application.DTOs;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace InvoiceAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]

public class InvoicesController : ControllerBase
{
    private readonly AppDbContext _db;

    // Constructor injection — the DI container supplies this
    public InvoicesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvoiceSummaryDto>>> GetAll(CancellationToken ct)
    {
        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .OrderByDescending(i => i.IssueDate)
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
            return NotFound(new { message = $"Invoice {id} not found." });

        return Ok(MapToSummary(invoice));
    }

    private static InvoiceSummaryDto MapToSummary(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.IssueDate,
        invoice.Supplier?.Name,
        invoice.Total,
        invoice.Currency,
        invoice.Status.ToString()
    );
}