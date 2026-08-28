namespace InvoiceAI.Domain.Entities;

public class Invoice
{
    public int Id { get; set; }

    public string? InvoiceNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }

    // Many invoices -> one supplier
    public int? SupplierId { get; set; }
    public Party? Supplier { get; set; }

    public int? CustomerId { get; set; }
    public Party? Customer { get; set; }

    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "TND";

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Uploaded;

    // One invoice -> many line items
    public List<InvoiceLineItem> LineItems { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}