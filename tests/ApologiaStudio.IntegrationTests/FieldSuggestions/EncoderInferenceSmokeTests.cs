using System.Diagnostics;
using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

namespace ApologiaStudio.IntegrationTests.FieldSuggestions;

/// <summary>
/// One real pass over the two frozen artifacts, through the whole seam.
/// </summary>
/// <remarks>
/// This proves the seam, not the models. EVAL-6 measured quality over 886
/// documents and the V2.1 handoff measured latency; neither is repeated here.
/// What is checked is that Apologia can serialize, call, threshold, map and
/// attribute a real inference without an aberration.
///
/// Opt-in: it needs the resident worker running.
/// </remarks>
public sealed class EncoderInferenceSmokeTests
{
    #region Methods

    [Fact]
    public async Task The_real_cascade_answers_through_the_whole_seam()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        using var httpClient = new HttpClient();
        var runtime = Runtime(httpClient);

        Assert.True(
            await runtime.IsAvailableAsync(CancellationToken.None),
            "the encoder worker is not answering");

        var plan = new GenreFormInferencePlan(runtime);
        var provider = new EncoderBackedFieldSuggestionProvider(plan);

        var started = Stopwatch.GetTimestamp();

        var batch = await provider.GetSuggestionsAsync(
            new FieldSuggestionRequest(
                Guid.NewGuid(),
                new FieldSuggestionEvidence(
                    "Réponse aux objections contre la foi chrétienne",
                    "Défense raisonnée de la doctrine face aux critiques.")),
            [ApologiaFieldId.GenreForm],
            CancellationToken.None);

        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var result = Assert.Single(batch.Results);

        Assert.Equal(ApologiaFieldId.GenreForm, result.FieldId);
        Assert.NotEqual(FieldSuggestionStatus.Failed, result.Status);
        Assert.NotEqual(FieldSuggestionStatus.Unavailable, result.Status);

        Assert.Equal(
            EncoderBackedFieldSuggestionProvider.ProviderId,
            result.Provenance.Provider);
        Assert.Equal(GenreFormInferencePlan.PlanId, result.Provenance.PlanId);
        Assert.StartsWith(
            "sha256:",
            result.Provenance.ModelVersion,
            StringComparison.Ordinal);

        // Every suggested value is a real encoder-predictable product code.
        Assert.All(
            result.Suggestions,
            suggestion =>
            {
                Assert.Contains(
                    suggestion.ValueId,
                    GenreFormEncoderLabelMap.OutputOrder);
                Assert.InRange(suggestion.Score, 0d, 1d);
            });

        // A gross sanity bound, not a benchmark: the handoff records a P99 of
        // 195 ms for the cascade on CPU.
        Assert.True(
            elapsed < 10_000,
            $"one cascade took {elapsed:F0} ms, which is an aberration");
    }

    [Fact]
    public async Task Both_artifacts_answer_with_twenty_four_bounded_scores()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        using var httpClient = new HttpClient();
        var runtime = Runtime(httpClient);

        foreach (var modelId in new[]
                 {
                     GenreFormInferencePlan.PrimaryModelId,
                     GenreFormInferencePlan.FallbackModelId
                 })
        {
            var result = await runtime.InferAsync(
                new EncoderInferenceRequest(
                    modelId,
                    "[TITLE] Sermons sur la grâce"),
                CancellationToken.None);

            Assert.Equal(modelId, result.ModelId);
            Assert.StartsWith("sha256:", result.ModelVersion, StringComparison.Ordinal);
            Assert.Equal(
                GenreFormEncoderLabelMap.OutputCount,
                result.Scores.Count);
            Assert.All(result.Scores, score => Assert.InRange(score, 0d, 1d));
        }
    }

    [Fact]
    public async Task The_semantic_fallback_really_runs_when_the_primary_is_silent()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        using var httpClient = new HttpClient();
        var plan = new GenreFormInferencePlan(Runtime(httpClient));

        // Two bibliographic titles on which XLM-R-large proposes nothing at
        // 0.47, observed against the real artifacts. They exercise the two
        // outcomes the fallback can produce.
        var rescued = await plan.RunAsync(
            EncoderBackedFieldSuggestionProvider.ProviderId,
            new FieldSuggestionEvidence("Divers", null),
            CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Succeeded, rescued.Status);
        Assert.Equal(
            GenreFormInferencePlan.FallbackModelId,
            rescued.Provenance.ModelId);
        Assert.All(
            rescued.Suggestions,
            x => Assert.True(x.Score >= GenreFormInferencePlan.FallbackThreshold));

        var silent = await plan.RunAsync(
            EncoderBackedFieldSuggestionProvider.ProviderId,
            new FieldSuggestionEvidence("12 rue des Lilas", null),
            CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.NoSuggestion, silent.Status);
        Assert.Empty(silent.Suggestions);

        // Both models ran, and the provenance says which one decided.
        Assert.Equal(
            GenreFormInferencePlan.FallbackModelId,
            silent.Provenance.ModelId);
    }

    [Fact]
    public async Task An_editorial_draft_reaches_the_review_workflow_end_to_end()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        using var httpClient = new HttpClient();

        // The wiring the review panel uses, minus Blazor: draft -> evidence
        // policy -> capability -> cascade -> advisory analysis.
        var service = new GenreFormFieldSuggestionService(
            new EncoderBackedFieldSuggestionProvider(
                new GenreFormInferencePlan(Runtime(httpClient))));

        var analysis = await service.AnalyzeAsync(
            Draft(
                "Réponse aux objections contre la foi chrétienne",
                DocumentManagerEditorialDraftFactory.ImportedTitleOrigin,
                "Défense raisonnée de la doctrine face aux critiques."),
            CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Succeeded, analysis.Status);

        var identity = analysis.Result!.Identity;
        Assert.Equal(ApologiaGenreFormTaxonomy.Version, identity.PolicyVersion);
        Assert.Equal(
            EncoderBackedFieldSuggestionProvider.ProviderId,
            identity.ModelProvider);
        Assert.Equal(GenreFormInferencePlan.PlanId, identity.PlanId);
        Assert.StartsWith("sha256:", identity.ModelVersion, StringComparison.Ordinal);
        Assert.Null(identity.PromptVersion);

        var suggestion = Assert.Single(analysis.Result.Suggested);
        Assert.Equal("apologetic_writing", suggestion.Code);
        Assert.Equal("Apologetic writing", suggestion.PreferredLabel);
        Assert.NotNull(suggestion.Score);
        Assert.InRange(suggestion.Score!.Value, 0d, 1d);

        // A file-name title is not document metadata, so the same draft with
        // no other evidence has nothing to classify.
        var unavailable = await service.AnalyzeAsync(
            Draft(
                "scan_0012.pdf",
                DocumentManagerEditorialDraftFactory.FileNameTitleOrigin,
                null),
            CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Unavailable, unavailable.Status);
    }

    private static DocumentManagerEditorialDraft Draft(
        string title,
        string titleOrigin,
        string? description) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            new string('a', 64),
            "source.pdf",
            title,
            titleOrigin,
            PrimaryContributorName: null,
            PrimaryContributorRole: null,
            LanguageCode: null,
            EditionStatement: null,
            PublicationYear: null,
            PublicationPlace: null,
            description,
            DocumentManagerEditorialDraftStatus.PendingReview,
            Version: 0,
            LastEditedByUserId: null,
            ReviewedByUserId: null,
            ReviewedAtUtc: null,
            RejectionReason: null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Parts: [],
            GenreForms: []);

    [Fact]
    public async Task An_unreachable_worker_is_unavailable_not_failed()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        var runtime = new HttpEncoderInferenceRuntime(
            httpClient,
            new EncoderInferenceOptions(
                new Uri("http://127.0.0.1:5199/"),
                TimeSpan.FromSeconds(2)));

        Assert.False(await runtime.IsAvailableAsync(CancellationToken.None));

        var result = await new GenreFormInferencePlan(runtime).RunAsync(
            EncoderBackedFieldSuggestionProvider.ProviderId,
            new FieldSuggestionEvidence("A Defence of the Faith", null),
            CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Unavailable, result.Status);
    }

    #endregion

    #region Methods Helpers

    private static IEncoderInferenceRuntime Runtime(HttpClient httpClient)
    {
        var options = new EncoderInferenceOptions(
            LiveEncoderIntegrationGate.Endpoint(),
            TimeSpan.FromSeconds(60));

        httpClient.Timeout = options.Timeout;

        return new HttpEncoderInferenceRuntime(httpClient, options);
    }

    #endregion
}
