namespace InvoiceAI.Application.DTOs;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public class InvoiceFilterDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Supplier { get; set; }
    public decimal? MinTotal { get; set; }
    public decimal? MaxTotal { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record StatusCountDto(string Status, int Count);

public record SupplierStatDto(string Supplier, int Count, decimal Total);

public record DashboardStatsDto(
    int TotalInvoices,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<SupplierStatDto> TopSuppliers,
    int AccuracySampleSize,
    int FieldsCompared,
    int FieldsCorrect,
    double? AccuracyRate);