namespace InvoiceAI.Domain.Entities;

public class InvoiceLineItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? VatRate { get; set; }
    public decimal LineTotal { get; set; }
}