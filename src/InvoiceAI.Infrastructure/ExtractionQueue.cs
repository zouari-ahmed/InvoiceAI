using System.Threading.Channels;
using InvoiceAI.Application;

namespace InvoiceAI.Infrastructure.Processing;

public class ExtractionQueue : IExtractionQueue
{
    private readonly Channel<int> _channel;

    public ExtractionQueue(int capacity = 500)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<int>(options);
    }

    public ValueTask EnqueueAsync(int invoiceId, CancellationToken ct) =>
        _channel.Writer.WriteAsync(invoiceId, ct);

    public ValueTask<int> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}