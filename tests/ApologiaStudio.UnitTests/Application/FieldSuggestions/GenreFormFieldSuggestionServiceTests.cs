using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Application.Knowledge.MetadataReview;

namespace ApologiaStudio.UnitTests.Application.FieldSuggestions;

/// <summary>
/// The Genre/Form review path, driven by a scripted suggestion capability.
/// </summary>
/// <remarks>
/// No model runs here. What is under test is the translation between the
/// generic capability and the advisory workflow: which statuses become an
/// advisory record, which product terms are acceptable, and what a reviewer is
/// told when nothing ran.
/// </remarks>
public sealed class GenreFormFieldSuggestionServiceTests
{
    #region Methods Statuses

    [Fact]
    public async Task Suggestions_are_projected_onto_product_terms()
    {
        var analysis = await Analyze(
            Provider.Succeeding(("apologetic_writing", 0.91), ("essays", 0.52)));

        Assert.Equal(FieldSuggestionStatus.Succeeded, analysis.Status);
        Assert.Null(analysis.FailureReason);

        var suggestions = analysis.Result!.Suggested;

        Assert.Equal(
            ["apologetic_writing", "essays"],
            suggestions.Select(x => x.Code));
        Assert.Equal(
            ["Apologetic writing", "Essays"],
            suggestions.Select(x => x.PreferredLabel));
        Assert.Equal([0.91, 0.52], suggestions.Select(x => x.Score));

        // An encoder proposes; it does not argue.
        Assert.All(suggestions, x => Assert.Null(x.Justification));
        Assert.All(suggestions, x => Assert.Empty(x.Evidence));
        Assert.False(analysis.Result.InsufficientEvidence);
    }

    [Fact]
    public async Task A_completed_analysis_with_no_term_is_still_an_analysis()
    {
        // The reviewer is told a judgement was made, and history records it.
        var analysis = await Analyze(Provider.NoSuggestion());

        Assert.Equal(FieldSuggestionStatus.NoSuggestion, analysis.Status);
        Assert.NotNull(analysis.Result);
        Assert.Empty(analysis.Result.Suggested);
    }

    [Fact]
    public async Task Unavailable_is_not_a_conclusion_about_the_work()
    {
        var analysis = await Analyze(Provider.Unavailable());

        Assert.Equal(FieldSuggestionStatus.Unavailable, analysis.Status);

        // No advisory record: nothing ran, so there is nothing to keep.
        Assert.Null(analysis.Result);
    }

    [Fact]
    public async Task Failed_is_not_a_conclusion_about_the_work_either()
    {
        var analysis = await Analyze(Provider.Failed());

        Assert.Equal(FieldSuggestionStatus.Failed, analysis.Status);
        Assert.Null(analysis.Result);
    }

    [Fact]
    public async Task The_three_negatives_stay_distinguishable()
    {
        Assert.Equal(
            3,
            new[]
                {
                    (await Analyze(Provider.NoSuggestion())).Status,
                    (await Analyze(Provider.Unavailable())).Status,
                    (await Analyze(Provider.Failed())).Status
                }
                .Distinct()
                .Count());
    }

    #endregion

    #region Methods Untrusted Provider

    [Fact]
    public async Task An_unknown_product_code_fails_the_whole_analysis()
    {
        // A provider that returned one impossible code gives no reason to
        // trust the others.
        var analysis = await Analyze(
            Provider.Succeeding(("apologetic_writing", 0.9), ("not_a_genre", 0.8)));

        Assert.Equal(FieldSuggestionStatus.Failed, analysis.Status);
        Assert.Null(analysis.Result);
        Assert.Contains(
            "not_a_genre",
            analysis.FailureReason!,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("study_guide")]
    [InlineData("training_material")]
    [InlineData("instructional_lesson")]
    public async Task A_manual_only_term_can_never_arrive_from_a_machine(string code)
    {
        var analysis = await Analyze(Provider.Succeeding((code, 0.99)));

        Assert.Equal(FieldSuggestionStatus.Failed, analysis.Status);
        Assert.Contains(
            "reviewer",
            analysis.FailureReason!,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_lcgft_identity_is_refused_like_any_unknown_code()
    {
        var analysis = await Analyze(Provider.Succeeding(("gf2015026027", 0.9)));

        Assert.Equal(FieldSuggestionStatus.Failed, analysis.Status);
    }

    [Fact]
    public async Task An_answered_analysis_without_a_model_identity_fails()
    {
        var analysis = await Analyze(
            Provider.Succeeding(("sermon", 0.9)) with { ModelId = null });

        Assert.Equal(FieldSuggestionStatus.Failed, analysis.Status);
    }

    [Fact]
    public async Task An_unanswered_field_is_refused()
    {
        await Assert.ThrowsAsync<FieldSuggestionContractException>(
            () => Analyze(Provider.Silent()));
    }

    #endregion

    #region Methods Provenance

    [Fact]
    public async Task Provenance_carries_the_encoder_identity()
    {
        var analysis = await Analyze(Provider.Succeeding(("sermon", 0.88)));

        var identity = analysis.Result!.Identity;

        Assert.Equal("apologia-genre-form-v1", identity.PolicyVersion);
        Assert.Equal("encoder", identity.ModelProvider);
        Assert.Equal("genre-form-cascade/v2.1", identity.PlanId);
        Assert.Equal("mdeberta-v3-base", identity.ModelName);
        Assert.Equal("sha256:d188dc52", identity.ModelVersion);

        // An encoder has no prompt; the slot stays empty rather than borrowed.
        Assert.Null(identity.PromptVersion);
    }

    #endregion

    #region Methods Evidence

    [Fact]
    public async Task An_imported_title_reaches_the_capability()
    {
        var provider = Provider.NoSuggestion();

        await Analyze(
            provider,
            Draft(
                "Réponse aux objections",
                DocumentManagerEditorialDraftFactory.ImportedTitleOrigin,
                "Défense raisonnée."));

        Assert.Equal("Réponse aux objections", provider.SeenEvidence!.Title);
        Assert.Equal("Défense raisonnée.", provider.SeenEvidence.Description);
    }

    [Fact]
    public async Task A_file_name_never_reaches_the_capability_as_a_title()
    {
        var provider = Provider.NoSuggestion();

        await Analyze(
            provider,
            Draft(
                "apologetique-tome-2-final-v3",
                DocumentManagerEditorialDraftFactory.FileNameTitleOrigin,
                "Défense raisonnée."));

        Assert.Null(provider.SeenEvidence!.Title);
        Assert.Equal("Défense raisonnée.", provider.SeenEvidence.Description);
    }

    [Fact]
    public async Task A_draft_with_nothing_to_classify_is_handed_over_empty()
    {
        // The capability answers Unavailable on empty evidence; the service
        // does not pre-empt that decision.
        var provider = Provider.Unavailable();

        var analysis = await Analyze(
            provider,
            Draft(
                "scan_0012.pdf",
                DocumentManagerEditorialDraftFactory.FileNameTitleOrigin,
                null));

        Assert.True(provider.SeenEvidence!.IsEmpty);
        Assert.Equal(FieldSuggestionStatus.Unavailable, analysis.Status);
    }

    [Fact]
    public async Task Only_genre_form_is_requested()
    {
        var provider = Provider.NoSuggestion();

        await Analyze(provider);

        Assert.Equal(
            [ApologiaFieldId.GenreForm],
            provider.SeenFields);
    }

    #endregion

    #region Methods Reviewer Outcome

    [Fact]
    public void The_reviewer_outcome_semantics_are_unchanged()
    {
        // Section 8 of the slice, expressed against the encoder's output.
        Assert.Equal(
            MetadataReviewOutcome.Accepted,
            MetadataReviewOutcomeCalculator.Determine(
                ["apologetic_writing"],
                ["apologetic_writing"]));

        Assert.Equal(
            MetadataReviewOutcome.Modified,
            MetadataReviewOutcomeCalculator.Determine(
                ["apologetic_writing"],
                ["apologetic_writing", "textbook"]));

        Assert.Equal(
            MetadataReviewOutcome.Rejected,
            MetadataReviewOutcomeCalculator.Determine(
                ["apologetic_writing"],
                []));

        // A completed analysis that proposed nothing, then a manual choice:
        // the reviewer changed an empty proposal, which is a modification.
        Assert.Equal(
            MetadataReviewOutcome.Modified,
            MetadataReviewOutcomeCalculator.Determine([], ["sermon"]));

        // Agreeing that nothing applies stays an acceptance.
        Assert.Equal(
            MetadataReviewOutcome.Accepted,
            MetadataReviewOutcomeCalculator.Determine([], []));
    }

    [Fact]
    public async Task A_successful_analysis_is_recorded_as_advisory_history()
    {
        var store = new RecordingStore();

        var analysis = await new GenreFormFieldSuggestionService(
                Provider.Succeeding(("sermon", 0.9)),
                store)
            .AnalyzeAndRecordAsync(Draft(), Actor, CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Succeeded, analysis.Status);
        Assert.NotNull(analysis.AnalysisId);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(Actor, recorded.ActorUserId);
        Assert.Equal(
            "sermon",
            Assert.Single(recorded.Result.Suggested).Code);
        Assert.Empty(store.Failures);
    }

    [Fact]
    public async Task A_completed_analysis_with_no_term_is_still_recorded()
    {
        var store = new RecordingStore();

        var analysis = await new GenreFormFieldSuggestionService(
                Provider.NoSuggestion(),
                store)
            .AnalyzeAndRecordAsync(Draft(), Actor, CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.NoSuggestion, analysis.Status);
        Assert.NotNull(analysis.AnalysisId);
        Assert.Empty(Assert.Single(store.Recorded).Result.Suggested);
    }

    [Fact]
    public async Task Nothing_is_recorded_when_nothing_ran()
    {
        // Unavailable is not a result. Persisting one would put a judgement in
        // history that no model ever made.
        var store = new RecordingStore();

        var analysis = await new GenreFormFieldSuggestionService(
                Provider.Unavailable(),
                store)
            .AnalyzeAndRecordAsync(Draft(), Actor, CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Unavailable, analysis.Status);
        Assert.Null(analysis.AnalysisId);
        Assert.Empty(store.Recorded);
        Assert.Empty(store.Failures);
    }

    [Fact]
    public async Task A_failure_is_recorded_as_a_failed_run()
    {
        var store = new RecordingStore();

        var analysis = await new GenreFormFieldSuggestionService(
                Provider.Failed(),
                store)
            .AnalyzeAndRecordAsync(Draft(), Actor, CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Failed, analysis.Status);
        Assert.Empty(store.Recorded);
        Assert.Single(store.Failures);
    }

    [Fact]
    public async Task A_history_write_that_fails_does_not_lose_the_analysis()
    {
        // Losing an advisory record costs evaluation data; failing the caller
        // for it would cost the analysis, and in the automatic case would put a
        // background service in the business of retrying a database write.
        var analysis = await new GenreFormFieldSuggestionService(
                Provider.Succeeding(("sermon", 0.9)),
                new ThrowingStore())
            .AnalyzeAndRecordAsync(Draft(), Actor, CancellationToken.None);

        Assert.Equal(FieldSuggestionStatus.Succeeded, analysis.Status);
        Assert.Null(analysis.AnalysisId);
        Assert.Single(analysis.Result!.Suggested);
    }

    #endregion

    #region Methods Helpers

    private static readonly Guid Actor =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static DocumentManagerEditorialDraft Draft() =>
        Draft(
            "Réponse aux objections",
            DocumentManagerEditorialDraftFactory.ImportedTitleOrigin,
            "Défense raisonnée.");

    private sealed class ThrowingStore : IMetadataReviewAnalysisStore
    {
        public Task<MetadataReviewAnalysis> RecordAsync(
            RecordMetadataReviewAnalysisCommand command,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("history is down");

        public Task<MetadataReviewAnalysis> RecordFailureAsync(
            RecordFailedMetadataReviewAnalysisCommand command,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("history is down");

        public Task<MetadataReviewAnalysis?> GetCurrentAsync(
            Guid draftId,
            string field,
            CancellationToken cancellationToken) =>
            Task.FromResult<MetadataReviewAnalysis?>(null);

        public Task<IReadOnlyList<MetadataReviewAnalysis>> ListAsync(
            Guid draftId,
            string field,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MetadataReviewAnalysis>>([]);

        public Task RecordReviewerOutcomeAsync(
            Guid analysisId,
            MetadataReviewOutcome outcome,
            Guid reviewerUserId,
            DateTimeOffset reviewedAtUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static Task<GenreFormFieldAnalysis> Analyze(
        Provider provider,
        DocumentManagerEditorialDraft? draft = null) =>
        new GenreFormFieldSuggestionService(provider, new RecordingStore()).AnalyzeAsync(
            draft ?? Draft(
                "Réponse aux objections",
                DocumentManagerEditorialDraftFactory.ImportedTitleOrigin,
                "Défense raisonnée de la doctrine."),
            CancellationToken.None);

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

    /// <summary>
    /// Accepts advisory records and remembers them, so what history would keep
    /// can be asserted without a database.
    /// </summary>
    private sealed class RecordingStore : IMetadataReviewAnalysisStore
    {
        public List<RecordMetadataReviewAnalysisCommand> Recorded { get; } = [];

        public List<RecordFailedMetadataReviewAnalysisCommand> Failures { get; } = [];

        public MetadataReviewAnalysis? Current { get; set; }

        public Task<MetadataReviewAnalysis> RecordAsync(
            RecordMetadataReviewAnalysisCommand command,
            CancellationToken cancellationToken)
        {
            Recorded.Add(command);
            return Task.FromResult(Analysis(MetadataReviewAnalysisStatus.Valid));
        }

        public Task<MetadataReviewAnalysis> RecordFailureAsync(
            RecordFailedMetadataReviewAnalysisCommand command,
            CancellationToken cancellationToken)
        {
            Failures.Add(command);
            return Task.FromResult(Analysis(MetadataReviewAnalysisStatus.Failed));
        }

        public Task<MetadataReviewAnalysis?> GetCurrentAsync(
            Guid draftId,
            string field,
            CancellationToken cancellationToken) =>
            Task.FromResult(Current);

        public Task<IReadOnlyList<MetadataReviewAnalysis>> ListAsync(
            Guid draftId,
            string field,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MetadataReviewAnalysis>>([]);

        public Task RecordReviewerOutcomeAsync(
            Guid analysisId,
            MetadataReviewOutcome outcome,
            Guid reviewerUserId,
            DateTimeOffset reviewedAtUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        private static MetadataReviewAnalysis Analysis(
            MetadataReviewAnalysisStatus status) =>
            new(
                Guid.CreateVersion7(),
                Guid.NewGuid(),
                MetadataReviewAnalysis.GenreFormField,
                status,
                PolicyVersion: null,
                PromptVersion: null,
                PlanId: null,
                ModelProvider: null,
                ModelName: null,
                ModelVersion: null,
                InsufficientEvidence: false,
                FailureReason: null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DurationMilliseconds: null,
                Guid.NewGuid(),
                SupersededByAnalysisId: null,
                ReviewerOutcome: null,
                ReviewerUserId: null,
                ReviewedAtUtc: null,
                Suggestions: []);
    }

    private sealed record Provider(
        FieldSuggestionStatus Status,
        IReadOnlyList<(string Code, double Score)> Values,
        bool Answer = true,
        string? ModelId = "mdeberta-v3-base")
        : IFieldSuggestionProvider
    {
        public FieldSuggestionEvidence? SeenEvidence { get; private set; }

        public IReadOnlyList<ApologiaFieldId>? SeenFields { get; private set; }

        public static Provider Succeeding(
            params (string Code, double Score)[] values) =>
            new(FieldSuggestionStatus.Succeeded, values);

        public static Provider NoSuggestion() =>
            new(FieldSuggestionStatus.NoSuggestion, []);

        public static Provider Unavailable() =>
            new(FieldSuggestionStatus.Unavailable, [], ModelId: null);

        public static Provider Failed() =>
            new(FieldSuggestionStatus.Failed, [], ModelId: "xlm-roberta-large");

        /// <summary>A provider that omits the field it was asked about.</summary>
        public static Provider Silent() =>
            new(FieldSuggestionStatus.NoSuggestion, [], Answer: false);

        public Task<FieldSuggestionBatch> GetSuggestionsAsync(
            FieldSuggestionRequest request,
            IReadOnlyCollection<ApologiaFieldId>? fields = null,
            CancellationToken cancellationToken = default)
        {
            SeenEvidence = request.Evidence;
            SeenFields = fields?.ToList();

            if (!Answer)
            {
                return Task.FromResult(FieldSuggestionBatch.Empty);
            }

            var provenance = new FieldSuggestionProvenance(
                "encoder",
                "genre-form-cascade/v2.1",
                ModelId,
                ModelId is null ? null : "sha256:d188dc52");

            return Task.FromResult(
                new FieldSuggestionBatch(
                [
                    new FieldSuggestionResult(
                        ApologiaFieldId.GenreForm,
                        Status,
                        Values
                            .Select(x => new FieldSuggestion(x.Code, x.Score))
                            .ToList(),
                        provenance)
                ]));
        }
    }

    #endregion
}
