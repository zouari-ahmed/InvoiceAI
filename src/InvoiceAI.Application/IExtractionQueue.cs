namespace InvoiceAI.Application;

public interface IExtractionQueue
{
    ValueTask EnqueueAsync(int invoiceId, CancellationToken ct);

    ValueTask<int> DequeueAsync(CancellationToken ct);
}