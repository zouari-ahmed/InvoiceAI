using InvoiceAI.Application;

namespace InvoiceAI.Api;

public class ExtractionBackgroundService : BackgroundService
{
    private readonly IExtractionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExtractionBackgroundService> _logger;

    public ExtractionBackgroundService(
        IExtractionQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ExtractionBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Extraction worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            int invoiceId;

            try
            {
                invoiceId = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IInvoiceProcessor>();
                await processor.ProcessAsync(invoiceId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error processing invoice {InvoiceId}. The worker continues.",
                    invoiceId);
            }
        }

        _logger.LogInformation("Extraction worker stopped.");
    }
}