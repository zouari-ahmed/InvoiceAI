namespace InvoiceAI.Application.DTOs;

public record InvoiceSummaryDto(
    int Id,
    string? InvoiceNumber,
    DateOnly? IssueDate,
    string? SupplierName,
    decimal Total,
    string Currency,
    string Status
);