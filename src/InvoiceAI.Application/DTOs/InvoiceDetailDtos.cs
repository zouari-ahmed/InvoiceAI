namespace InvoiceAI.Application.DTOs;

public record PartyDto(int Id, string Name, string? Address, string? TaxId);

public record LineItemDto(
    int Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal? VatRate,
    decimal? LineTotal);

public record InvoiceDetailDto(
    int Id,
    string? InvoiceNumber,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    PartyDto? Supplier,
    PartyDto? Customer,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    string Currency,
    string Status,
    string? OriginalFileName,
    string? ContentType,
        DateTime? CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<LineItemDto> LineItems);

public record UpdatePartyDto(string? Name, string? Address, string? TaxId);

public record UpdateLineItemDto(
    string? Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal? VatRate,
    decimal? LineTotal);

public record UpdateInvoiceDto(
    string? InvoiceNumber,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    UpdatePartyDto? Supplier,
    UpdatePartyDto? Customer,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    string? Currency,
    List<UpdateLineItemDto>? LineItems);

public record CorrectionStatsDto(int TotalFields, int CorrectedFields, double AccuracyRate);