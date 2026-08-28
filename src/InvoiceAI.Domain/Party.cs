namespace InvoiceAI.Domain.Entities;

public class Party
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? TaxId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}