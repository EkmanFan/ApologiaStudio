using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

namespace ApologiaStudio.Web.FieldSuggestions;

/// <summary>
/// Makes the encoder worker available without the user having to think about
/// it, and keeps Apologia working when it is not.
/// </summary>
/// <remarks>
/// The smallest useful supervisor, deliberately: probe, start if absent, wait a
/// bounded moment, then watch. It is not an orchestrator and must not become
/// one.
///
/// Two rules matter more than the mechanics. Apologia's startup never depends
/// on the encoder — every failure below is a warning and nothing else. And a
/// worker Apologia did not start is never one Apologia stops: taking ownership
/// of someone else's process is how a development machine loses a session it
/// was using.
/// </remarks>
public sealed class EncoderWorkerSupervisor(
    IEncoderWorkerHost host,
    EncoderWorkerOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<EncoderWorkerSupervisor> logger)
    : BackgroundService
{
    #region Variables and Constants

    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1);

    private bool _owned;

    #endregion

    #region Methods

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CanAutoStart)
        {
            logger.LogInformation(
                "Encoder worker auto-start is not configured; genre/form " +
                "suggestions stay unavailable unless a worker is reachable.");
            return;
        }

        try
        {
            if (!await StartOwnedWorkerAsync(stoppingToken))
            {
                return;
            }

            await WatchAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Ordinary shutdown.
        }
        catch (Exception exception)
        {
            // An auxiliary capability must never take the application with it.
            logger.LogWarning(
                exception,
                "Encoder worker supervision stopped; genre/form suggestions " +
                "may be unavailable.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (!_owned)
        {
            return;
        }

        try
        {
            await host.StopAsync(cancellationToken);
            logger.LogInformation("Encoder worker stopped.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Encoder worker did not stop cleanly.");
        }
    }

    #endregion

    #region Methods Lifecycle

    /// <summary>
    /// Ensures a worker is reachable, starting one only if none answers.
    /// </summary>
    /// <returns>Whether a worker started here is worth watching.</returns>
    private async Task<bool> StartOwnedWorkerAsync(
        CancellationToken cancellationToken)
    {
        if (await IsHealthyAsync(cancellationToken))
        {
            logger.LogInformation(
                "An encoder worker is already running; Apologia will use it " +
                "and will not stop it.");
            return false;
        }

        logger.LogInformation("Starting the encoder worker.");

        var outcome = await host.StartAsync(cancellationToken);

        if (outcome != EncoderWorkerStartOutcome.Started)
        {
            logger.LogWarning(
                "The encoder worker could not be started ({Outcome}); " +
                "genre/form suggestions stay unavailable and manual review " +
                "is unaffected.",
                outcome);
            return false;
        }

        _owned = true;

        if (await WaitForReadyAsync(options.StartupTimeout, cancellationToken))
        {
            logger.LogInformation("Encoder worker ready.");
        }
        else
        {
            logger.LogWarning(
                "The encoder worker did not become ready within {Timeout}; " +
                "genre/form suggestions stay unavailable until it does.",
                options.StartupTimeout);
        }

        return true;
    }

    /// <summary>
    /// Restarts the worker we own if it dies, with a bounded backoff.
    /// </summary>
    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        var backoff = options.MinimumRestartBackoff;

        while (!cancellationToken.IsCancellationRequested)
        {
            // The liveness poll runs at the shortest interval a restart could
            // usefully take: checking faster than we would ever act is noise.
            await Task.Delay(options.MinimumRestartBackoff, cancellationToken);

            if (host.IsRunning)
            {
                backoff = options.MinimumRestartBackoff;
                continue;
            }

            if (!options.RestartEnabled)
            {
                logger.LogWarning(
                    "The encoder worker stopped and restart is disabled; " +
                    "genre/form suggestions stay unavailable.");
                return;
            }

            logger.LogWarning(
                "The encoder worker stopped; restarting in {Backoff}.",
                backoff);

            await Task.Delay(backoff, cancellationToken);

            if (await host.StartAsync(cancellationToken)
                    == EncoderWorkerStartOutcome.Started &&
                await WaitForReadyAsync(options.StartupTimeout, cancellationToken))
            {
                logger.LogInformation("Encoder worker restarted and ready.");
                backoff = options.MinimumRestartBackoff;
                continue;
            }

            logger.LogWarning("The encoder worker restart did not succeed.");

            // Bounded, so a machine without Docker is not asked forever.
            backoff = backoff + backoff > options.MaximumRestartBackoff
                ? options.MaximumRestartBackoff
                : backoff + backoff;
        }
    }

    #endregion

    #region Methods Health

    private async Task<bool> WaitForReadyAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await IsHealthyAsync(cancellationToken))
            {
                return true;
            }

            await Task.Delay(ProbeInterval, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Health is whatever the inference runtime already calls available, so
    /// there is one definition of a usable worker.
    /// </summary>
    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            return await scope.ServiceProvider
                .GetRequiredService<IEncoderInferenceRuntime>()
                .IsAvailableAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    #endregion
}
