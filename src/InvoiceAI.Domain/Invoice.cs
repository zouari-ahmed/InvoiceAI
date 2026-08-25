namespace InvoiceAI.Domain.Entities;

public class Invoice
{
    public int Id { get; set; }

    // Header fields — nullable because extraction may not find them
    public string? InvoiceNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }

    public string? SupplierName { get; set; }
    public string? SupplierTaxId { get; set; }

    // Money. decimal, never double — see the note below.
    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "TND";

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Uploaded;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}