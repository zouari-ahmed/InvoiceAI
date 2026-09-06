namespace InvoiceAI.Application;

public interface IInvoiceProcessor
{
    Task<bool> ProcessAsync(int invoiceId, CancellationToken ct);
}