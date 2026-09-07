using InvoiceAI.Application;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Infrastructure.Data;
using InvoiceAI.Infrastructure.Gemini;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvoiceAI.Api;

public class ExtractionBackgroundService : BackgroundService
{
    private readonly IExtractionQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GeminiOptions _gemini;
    private readonly ILogger<ExtractionBackgroundService> _logger;

    public ExtractionBackgroundService(
        IExtractionQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<GeminiOptions> gemini,
        ILogger<ExtractionBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _gemini = gemini.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Extraction worker started.");

        await RequeueUnfinishedAsync(stoppingToken);

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

            if (_gemini.DelayBetweenJobsSeconds > 0)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_gemini.DelayBetweenJobsSeconds),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Extraction worker stopped.");
    }
    private async Task RequeueUnfinishedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var unfinished = await db.Invoices
                .Where(i => i.SourceFileId != null
                            && (i.Status == InvoiceStatus.Uploaded
                                || i.Status == InvoiceStatus.Processing))
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
                .ToListAsync(ct);

            foreach (var id in unfinished)
            {
                await _queue.EnqueueAsync(id, ct);
            }

            if (unfinished.Count > 0)
            {
                _logger.LogInformation(
                    "Re-queued {Count} unfinished invoice(s) left over from a previous run.",
                    unfinished.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not re-queue unfinished invoices at startup.");
        }
    }
}