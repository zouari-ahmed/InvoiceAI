using InvoiceAI.Application.DTOs;

namespace InvoiceAI.Application;

public interface IInvoiceExtractor
{
    Task<ExtractionResultDto> ExtractAsync(
        Stream document,
        string mimeType,
        CancellationToken ct);
}