using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

namespace ApologiaStudio.UnitTests.Infrastructure.FieldSuggestions;

/// <summary>
/// The frozen V2.1 cascade, exercised against a scripted runtime.
/// </summary>
/// <remarks>
/// No model is loaded here. What is under test is the orchestration the handoff
/// assigns to the application: thresholds, the semantic fallback condition, the
/// refusal to fall back on a technical failure, validation of untrusted output,
/// and the provenance of the decision actually returned.
/// </remarks>
public sealed class GenreFormInferencePlanTests
{
    #region Variables and Constants

    private const string Provider = "encoder";

    private static readonly FieldSuggestionEvidence Evidence =
        new("A Defence of the Faith", "A sustained apologetic essay.");

    #endregion

    #region Methods Primary

    [Fact]
    public async Task A_primary_result_above_the_threshold_is_returned_alone()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(("apologetic_writing", 0.91), ("essays", 0.55))
        };

        var result = await Run(runtime);

        Assert.Equal(FieldSuggestionStatus.Succeeded, result.Status);
        Assert.Equal(
            ["apologetic_writing", "essays"],
            result.Suggestions.Select(x => x.ValueId));
        Assert.Equal(0.91, result.Suggestions[0].Score);

        // The fallback exists for a silent primary, not to second-guess it.
        Assert.Equal([GenreFormInferencePlan.PrimaryModelId], runtime.Calls);
    }

    [Fact]
    public async Task The_primary_threshold_includes_its_own_boundary()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(("sermon", 0.47), ("prayer", 0.4699999))
        };

        var result = await Run(runtime);

        Assert.Equal(FieldSuggestionStatus.Succeeded, result.Status);
        Assert.Equal(
            "sermon",
            Assert.Single(result.Suggestions).ValueId);
    }

    [Fact]
    public async Task Suggestions_come_back_most_confident_first()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(
                ("essays", 0.52),
                ("apologetic_writing", 0.99),
                ("commentary", 0.75))
        };

        var result = await Run(runtime);

        Assert.Equal(
            ["apologetic_writing", "commentary", "essays"],
            result.Suggestions.Select(x => x.ValueId));
    }

    #endregion

    #region Methods Fallback

    [Fact]
    public async Task A_silent_primary_hands_over_to_the_fallback()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(("sermon", 0.46)),
            Fallback = Scores(("sermon", 0.61))
        };

        var result = await Run(runtime);

        Assert.Equal(FieldSuggestionStatus.Succeeded, result.Status);
        Assert.Equal("sermon", Assert.Single(result.Suggestions).ValueId);
        Assert.Equal(
            [
                GenreFormInferencePlan.PrimaryModelId,
                GenreFormInferencePlan.FallbackModelId
            ],
            runtime.Calls);
    }

    [Fact]
    public async Task The_fallback_threshold_includes_its_own_boundary()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(),
            Fallback = Scores(("creed", 0.43), ("prayer", 0.4299999))
        };

        var result = await Run(runtime);

        Assert.Equal(FieldSuggestionStatus.Succeeded, result.Status);
        Assert.Equal("creed", Assert.Single(result.Suggestions).ValueId);
    }

    [Fact]
    public async Task Two_silent_models_are_a_semantic_no_suggestion()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(("sermon", 0.2)),
            Fallback = Scores(("sermon", 0.42))
        };

        var result = await Run(runtime);

        Assert.Equal(FieldSuggestionStatus.NoSuggestion, result.Status);
        Assert.Empty(result.Suggestions);
        Assert.Equal(2, runtime.Calls.Count);
    }

    #endregion

    #region Methods Failure

    [Fact]
    public async Task A_broken_primary_never_falls_back()
    {
        // The fallback is semantic. Substituting a second model for a failed
        // first one would silently change which model answered.
        var runtime = new ScriptedRuntime
        {
            PrimaryFailure = new EncoderInferenceException("runtime blew up")
        };

        var result = await Run(runtime);

        Assert.Equal(FieldSuggestionStatus.Failed, result.Status);
        Assert.Equal([GenreFormInferencePlan.PrimaryModelId], runtime.Calls);
        Assert.Equal(
            GenreFormInferencePlan.PrimaryModelId,
            result.Provenance.ModelId);
    }

    [Fact]
    public async Task A_broken_fallback_after_a_silent_primary_fails()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(),
            FallbackFailure = new EncoderInferenceException("fallback blew up")
        };

        var result = await Run(runtime);

        Assert.Equal(FieldSuggestionStatus.Failed, result.Status);
        Assert.Equal(
            GenreFormInferencePlan.FallbackModelId,
            result.Provenance.ModelId);
    }

    [Fact]
    public async Task An_absent_runtime_is_unavailable_and_never_failed()
    {
        var result = await Run(new ScriptedRuntime { Available = false });

        Assert.Equal(FieldSuggestionStatus.Unavailable, result.Status);
        Assert.Null(result.Provenance.ModelId);
    }

    [Fact]
    public async Task An_unavailable_primary_is_unavailable_and_never_falls_back()
    {
        var runtime = new ScriptedRuntime
        {
            PrimaryFailure = new EncoderInferenceUnavailableException("gone")
        };

        var result = await Run(runtime);

        Assert.Equal(FieldSuggestionStatus.Unavailable, result.Status);
        Assert.Equal([GenreFormInferencePlan.PrimaryModelId], runtime.Calls);
    }

    [Fact]
    public async Task Evidence_with_nothing_to_classify_is_unavailable()
    {
        // Answering NoSuggestion would claim the encoder looked and judged.
        var runtime = new ScriptedRuntime { Primary = Scores(("sermon", 0.9)) };

        var result = await new GenreFormInferencePlan(runtime).RunAsync(
            Provider,
            FieldSuggestionEvidence.Empty,
            CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Unavailable, result.Status);
        Assert.Empty(runtime.Calls);
    }

    [Fact]
    public async Task Cancellation_stays_cancellation()
    {
        var runtime = new ScriptedRuntime { Primary = Scores(("sermon", 0.9)) };

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new GenreFormInferencePlan(runtime).RunAsync(
                Provider,
                Evidence,
                cancellation.Token));
    }

    #endregion

    #region Methods Untrusted Output

    [Theory]
    [InlineData(23)]
    [InlineData(25)]
    [InlineData(0)]
    public async Task A_wrong_output_count_fails(int count)
    {
        // A different output dimension means a different artifact, and every
        // index would then mean a different label.
        var runtime = new ScriptedRuntime
        {
            Primary = Enumerable.Repeat(0.9, count).ToList()
        };

        Assert.Equal(FieldSuggestionStatus.Failed, (await Run(runtime)).Status);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task A_score_that_cannot_be_thresholded_fails(double score)
    {
        var scores = Scores();
        scores[0] = score;

        Assert.Equal(
            FieldSuggestionStatus.Failed,
            (await Run(new ScriptedRuntime { Primary = scores })).Status);
    }

    [Fact]
    public async Task A_runtime_answering_as_another_model_fails()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(("sermon", 0.9)),
            AnswerAs = "some-other-model"
        };

        Assert.Equal(FieldSuggestionStatus.Failed, (await Run(runtime)).Status);
    }

    [Fact]
    public async Task A_runtime_without_an_artifact_version_fails()
    {
        var runtime = new ScriptedRuntime
        {
            Primary = Scores(("sermon", 0.9)),
            Version = "  "
        };

        Assert.Equal(FieldSuggestionStatus.Failed, (await Run(runtime)).Status);
    }

    #endregion

    #region Methods Provenance

    [Fact]
    public async Task Provenance_identifies_the_model_that_actually_decided()
    {
        var primary = await Run(
            new ScriptedRuntime { Primary = Scores(("sermon", 0.9)) });

        Assert.Equal(Provider, primary.Provenance.Provider);
        Assert.Equal(GenreFormInferencePlan.PlanId, primary.Provenance.PlanId);
        Assert.Equal(
            GenreFormInferencePlan.PrimaryModelId,
            primary.Provenance.ModelId);
        Assert.Equal("sha256:primary", primary.Provenance.ModelVersion);
        Assert.Null(primary.Provenance.HeadId);

        var rescued = await Run(
            new ScriptedRuntime
            {
                Primary = Scores(),
                Fallback = Scores(("sermon", 0.9))
            });

        // The model identity alone says the fallback ran: a result attributed
        // to mDeBERTa can only follow a primary that proposed nothing.
        Assert.Equal(
            GenreFormInferencePlan.FallbackModelId,
            rescued.Provenance.ModelId);
        Assert.Equal("sha256:fallback", rescued.Provenance.ModelVersion);
    }

    #endregion

    #region Methods Helpers

    private static Task<FieldSuggestionResult> Run(ScriptedRuntime runtime) =>
        new GenreFormInferencePlan(runtime).RunAsync(
            Provider,
            Evidence,
            CancellationToken.None);

    /// <summary>
    /// A full 24-wide output, zero everywhere except the named codes.
    /// </summary>
    private static List<double> Scores(params (string Code, double Score)[] set)
    {
        var scores = new List<double>(
            Enumerable.Repeat(0d, GenreFormEncoderLabelMap.OutputCount));

        foreach (var (code, score) in set)
        {
            scores[GenreFormEncoderLabelMap.OutputOrder
                .Select((value, index) => (value, index))
                .First(x => x.value == code)
                .index] = score;
        }

        return scores;
    }

    private sealed class ScriptedRuntime : IEncoderInferenceRuntime
    {
        public bool Available { get; init; } = true;

        public List<double>? Primary { get; init; }

        public List<double>? Fallback { get; init; }

        public Exception? PrimaryFailure { get; init; }

        public Exception? FallbackFailure { get; init; }

        public string? AnswerAs { get; init; }

        public string Version { get; init; } = string.Empty;

        public List<string> Calls { get; } = [];

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Available);
        }

        public Task<EncoderInferenceResult> InferAsync(
            EncoderInferenceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(request.ModelId);

            var isPrimary =
                request.ModelId == GenreFormInferencePlan.PrimaryModelId;

            var failure = isPrimary ? PrimaryFailure : FallbackFailure;
            if (failure is not null)
            {
                throw failure;
            }

            var scores = isPrimary ? Primary : Fallback;
            if (scores is null)
            {
                throw new EncoderInferenceException(
                    $"'{request.ModelId}' was not scripted.");
            }

            var version = string.IsNullOrEmpty(Version)
                ? isPrimary ? "sha256:primary" : "sha256:fallback"
                : Version;

            return Task.FromResult(
                new EncoderInferenceResult(
                    AnswerAs ?? request.ModelId,
                    version,
                    scores,
                    12.5));
        }
    }

    #endregion
}
