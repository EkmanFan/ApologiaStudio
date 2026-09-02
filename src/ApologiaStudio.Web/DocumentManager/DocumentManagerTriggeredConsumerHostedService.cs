using System.Data.Common;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;

namespace ApologiaStudio.Web.DocumentManager;

public sealed class DocumentManagerTriggeredConsumerHostedService(
    DocumentManagerConsumptionSignal signal,
    DocumentManagerConsumerOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentManagerTriggeredConsumerHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        signal.Notify();
        var reconciliation = ProduceReconciliationSignalsAsync(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await signal.WaitAsync(stoppingToken).ConfigureAwait(false);

                try
                {
                    await DrainAvailableResultsAsync(stoppingToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or DbException)
                {
                    logger.LogWarning(
                        exception,
                        "Document Manager consumption failed transiently; it will be retried.");
                    await Task.Delay(options.RetryInterval, stoppingToken)
                        .ConfigureAwait(false);
                    signal.Notify();
                }
                catch (DocumentManagerResultIntegrityException exception)
                {
                    logger.LogError(
                        exception,
                        "A Document Manager result failed integrity validation. Automatic rapid retries are suspended until reconciliation.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            try
            {
                await reconciliation.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }

    private async Task ProduceReconciliationSignalsAsync(
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(options.ReconciliationInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken)
                   .ConfigureAwait(false))
        {
            signal.Notify();
        }
    }

    private async Task DrainAvailableResultsAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider
                .GetRequiredService<ConsumeDocumentManagerResultHandler>();
            var result = await handler.HandleAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result.Status == DocumentManagerConsumeStatus.NoResultAvailable)
            {
                return;
            }

            logger.LogInformation(
                "Consumed and acknowledged Document Manager result {ResultReference} for submission {SubmissionId} ({Status}).",
                result.ResultReference,
                result.SubmissionId,
                result.Status);
        }
    }
}
