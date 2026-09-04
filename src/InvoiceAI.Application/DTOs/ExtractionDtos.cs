using System.Text.Json.Serialization;

namespace InvoiceAI.Application.DTOs;

public record ExtractedLineItemDto(
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("quantity")] decimal? Quantity,
    [property: JsonPropertyName("unit_price")] decimal? UnitPrice,
    [property: JsonPropertyName("vat_rate")] decimal? VatRate,
    [property: JsonPropertyName("line_total")] decimal? LineTotal);

public record ExtractedPartyDto(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("tax_id")] string? TaxId);

public record ExtractedInvoiceDto(
    [property: JsonPropertyName("invoice_number")] string? InvoiceNumber,
    [property: JsonPropertyName("issue_date")] string? IssueDate,
    [property: JsonPropertyName("due_date")] string? DueDate,
    [property: JsonPropertyName("supplier")] ExtractedPartyDto? Supplier,
    [property: JsonPropertyName("customer")] ExtractedPartyDto? Customer,
    [property: JsonPropertyName("line_items")] List<ExtractedLineItemDto>? LineItems,
    [property: JsonPropertyName("subtotal")] decimal? Subtotal,
    [property: JsonPropertyName("vat_amount")] decimal? VatAmount,
    [property: JsonPropertyName("total")] decimal? Total,
    [property: JsonPropertyName("currency")] string? Currency);

public record ExtractionResultDto(
    bool Success,
    ExtractedInvoiceDto? Invoice,
    string? RawJson,
    string? ErrorMessage);