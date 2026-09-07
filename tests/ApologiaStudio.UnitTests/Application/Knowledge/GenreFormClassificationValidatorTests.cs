using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

/// <summary>
/// Validation is deterministic and persistence-free: every case here runs
/// against a policy value, with no Work, no editorial draft and no database.
/// </summary>
/// <remarks>
/// The policy is the real canonical taxonomy rather than a hand-built fixture,
/// so a rule can never pass against a vocabulary the product does not ship.
/// </remarks>
public sealed class GenreFormClassificationValidatorTests
{
    #region Variables and Constants

    private static readonly GenreFormClassificationValidator Validator = new();

    #endregion

    #region Methods Vocabulary

    [Fact]
    public void Zero_suggestions_is_valid()
    {
        // AC-MRA-03: a legitimate Work may carry no applicable term.
        var validation = Validate(Raw());

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Result!.Suggested);
    }

    [Fact]
    public void Multiple_independent_terms_are_accepted()
    {
        // AC-MRA-04, reference case: the papacy essay.
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("apologetic_writing", "Sustained defence of a position."),
                    Suggestion("essays", "Essay form throughout.")
                ]));

        Assert.True(validation.IsValid);
        Assert.Equal(2, validation.Result!.Suggested.Count);
    }

    [Fact]
    public void An_invented_term_is_rejected_and_never_coerced()
    {
        // The model returns a plausible label rather than a product code.
        var validation = Validate(
            Raw(suggested: [Suggestion("Commentaries on the Psalms", "Looks apt.")]));

        Assert.False(validation.IsValid);
        Assert.Null(validation.Result);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.UnknownTerm);
    }

    [Fact]
    public void An_lcgft_identifier_is_not_a_product_term()
    {
        // Alignment is recorded elsewhere; it is never an identity the
        // assistant may answer with.
        var validation = Validate(
            Raw(suggested: [Suggestion("gf2014026094", "Essay form.")]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.UnknownTerm);
    }

    [Fact]
    public void A_manual_only_term_is_not_refused_by_validation()
    {
        // The prediction mode bounds the future encoder scope, not what may be
        // validated: a manual-only term is a legitimate product concept.
        var validation = Validate(
            Raw(suggested: [Suggestion("study_guide", "Structured study aid.")]));

        Assert.True(validation.IsValid);
        Assert.Equal(
            "study_guide",
            Assert.Single(validation.Result!.Suggested).Code);
    }

    [Fact]
    public void Two_terms_are_never_redundant_in_a_flat_taxonomy()
    {
        // Before GF-TAX-5 this pair was a hierarchy violation under LCGFT.
        // The product taxonomy declares no hierarchy, so both simply stand.
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("biography", "A life story."),
                    Suggestion("essays", "Written as essays.")
                ]));

        Assert.True(validation.IsValid);
        Assert.Equal(2, validation.Result!.Suggested.Count);
    }

    #endregion

    #region Methods Shape

    [Fact]
    public void A_duplicated_term_is_rejected()
    {
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("essays", "Essay form."),
                    Suggestion("essays", "Essay form again.")
                ]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.DuplicateSuggestion);
    }

    [Fact]
    public void A_term_cannot_be_suggested_and_rejected_at_once()
    {
        var validation = Validate(
            Raw(
                suggested: [Suggestion("essays", "Essay form.")],
                rejected: [Rejection("essays", "Not really an essay.")]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.SuggestedAndRejected);
    }

    [Fact]
    public void A_suggestion_without_justification_is_rejected()
    {
        // AC-MRA-06.
        var validation = Validate(
            Raw(suggested: [Suggestion("essays", "   ")]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.MissingJustification);
    }

    [Fact]
    public void A_rejection_without_reason_is_rejected()
    {
        var validation = Validate(
            Raw(rejected: [Rejection("essays", null)]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.MissingRejectionReason);
    }

    [Fact]
    public void Exceeding_the_cardinality_bound_is_rejected()
    {
        // A model returning most of the vocabulary is not classifying.
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("apologetic_writing", "One."),
                    Suggestion("essays", "Two."),
                    Suggestion("biography", "Three."),
                    Suggestion("textbook", "Four."),
                    Suggestion("sermon", "Five.")
                ]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.TooManySuggestions);
    }

    [Fact]
    public void Insufficient_evidence_cannot_accompany_a_suggestion()
    {
        var validation = Validate(
            Raw(
                suggested: [Suggestion("essays", "Essay form.")],
                insufficientEvidence: true));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.ContradictoryInsufficientEvidence);
    }

    [Fact]
    public void Insufficient_evidence_alone_is_valid()
    {
        var validation = Validate(Raw(insufficientEvidence: true));

        Assert.True(validation.IsValid);
        Assert.True(validation.Result!.InsufficientEvidence);
    }

    [Fact]
    public void A_missing_term_code_is_rejected()
    {
        var validation = Validate(
            Raw(suggested: [Suggestion(null, "No code at all.")]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.MissingTermCode);
    }

    [Fact]
    public void A_single_violation_discards_the_whole_classification()
    {
        // Fail closed: one invented term makes the rest untrustworthy.
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("essays", "Essay form."),
                    Suggestion("invented_genre", "Invented.")
                ]));

        Assert.False(validation.IsValid);
        Assert.Null(validation.Result);
    }

    [Fact]
    public void Validation_retains_policy_model_and_prompt_identity()
    {
        // AC-MRA-09.
        var validation = Validate(Raw());

        var identity = validation.Result!.Identity;
        Assert.Equal("apologia-genre-form-v1", identity.PolicyVersion);
        Assert.Equal("genre-form-classification/1", identity.PromptVersion);
        Assert.Equal("ollama", identity.ModelProvider);
    }

    #endregion

    #region Methods Helpers

    private static GenreFormClassificationValidation Validate(
        RawGenreFormClassification raw)
    {
        return Validator.Validate(raw, Policy(), Identity());
    }

    private static RawGenreFormClassification Raw(
        IReadOnlyList<RawGenreFormSuggestion>? suggested = null,
        IReadOnlyList<RawGenreFormRejection>? rejected = null,
        bool insufficientEvidence = false)
    {
        return new RawGenreFormClassification(
            suggested ?? [],
            rejected ?? [],
            insufficientEvidence);
    }

    private static RawGenreFormSuggestion Suggestion(
        string? termCode,
        string? justification)
    {
        return new RawGenreFormSuggestion(termCode, justification, []);
    }

    private static RawGenreFormRejection Rejection(
        string? termCode,
        string? reason)
    {
        return new RawGenreFormRejection(termCode, reason);
    }

    private static MetadataReviewAnalysisIdentity Identity()
    {
        return new MetadataReviewAnalysisIdentity(
            ApologiaGenreFormTaxonomy.Version,
            "genre-form-classification/1",
            "ollama",
            "qwen3.6:27b",
            new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// The canonical product taxonomy, projected exactly as the production
    /// provider projects it.
    /// </summary>
    private static GenreFormPolicySnapshot Policy()
    {
        return new GenreFormPolicySnapshot(
            ApologiaGenreFormTaxonomy.Version,
            ApologiaGenreFormTaxonomy.Terms
                .Select(x => new GenreFormPolicyTerm(
                    x.Code,
                    x.PreferredLabel,
                    x.PredictionMode))
                .ToList());
    }

    #endregion
}
