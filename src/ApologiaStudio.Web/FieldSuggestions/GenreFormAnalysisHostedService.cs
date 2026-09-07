using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Application.Knowledge.MetadataReview;

namespace ApologiaStudio.Web.FieldSuggestions;

/// <summary>
/// Runs the Genre/Form analysis of a newly created draft, out of band.
/// </summary>
/// <remarks>
/// So that suggestions are usually already there when a reviewer opens the
/// record, without the ingestion pipeline ever waiting for a model. Everything
/// here is best effort: a draft is created and reviewable whether or not this
/// service ever succeeds.
///
/// One attempt per draft. The encoder may still be loading its two models when
/// the first document arrives, and the honest answer then is "unavailable" —
/// retrying in a loop would turn a cold start into a stampede, and the reviewer
/// already has a button that runs one on demand.
/// </remarks>
public sealed class GenreFormAnalysisHostedService(
    GenreFormAnalysisQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<GenreFormAnalysisHostedService> logger)
    : BackgroundService
{
    #region Methods

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var draftId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await AnalyzeAsync(draftId, stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Never let one draft stop the service: the next document
                    // must still get its analysis.
                    logger.LogWarning(
                        exception,
                        "The automatic genre/form analysis of draft " +
                        "{DraftId} did not complete; manual review is " +
                        "unaffected.",
                        draftId);
                }
                finally
                {
                    queue.Release(draftId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ordinary shutdown.
        }
    }

    #endregion

    #region Methods Analysis

    private async Task AnalyzeAsync(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var drafts = scope.ServiceProvider
            .GetRequiredService<IDocumentManagerEditorialReviewStore>();

        var draft = await drafts.GetAsync(draftId, cancellationToken);

        if (draft is null)
        {
            return;
        }

        var analyses = scope.ServiceProvider
            .GetRequiredService<IMetadataReviewAnalysisStore>();

        // An analysis already stands for this draft, so the automatic run has
        // nothing to add. A reviewer who changed the title can still ask for a
        // fresh one from the panel.
        if (await analyses.GetCurrentAsync(
                draftId,
                MetadataReviewAnalysis.GenreFormField,
                cancellationToken) is not null)
        {
            return;
        }

        var analysis = await scope.ServiceProvider
            .GetRequiredService<GenreFormFieldSuggestionService>()
            .AnalyzeAndRecordAsync(draft, AutomaticActorId, cancellationToken);

        if (analysis.Status == FieldSuggestionStatus.Unavailable)
        {
            logger.LogInformation(
                "Genre/form assistance was unavailable for draft {DraftId}; " +
                "a reviewer can run it from the record.",
                draftId);
            return;
        }

        logger.LogInformation(
            "Automatic genre/form analysis of draft {DraftId} completed " +
            "({Status}).",
            draftId,
            analysis.Status);
    }

    /// <summary>
    /// The actor recorded for a run nobody requested by hand.
    /// </summary>
    /// <remarks>
    /// A fixed, recognisable identity rather than a real user: attributing an
    /// automatic run to whoever happened to trigger ingestion would put a name
    /// on a decision they did not make.
    /// </remarks>
    public static Guid AutomaticActorId { get; } =
        new("00000000-0000-0000-0000-00000000a17a");

    #endregion
}
