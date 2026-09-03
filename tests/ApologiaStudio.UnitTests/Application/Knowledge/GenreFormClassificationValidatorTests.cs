using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

/// <summary>
/// Validation is deterministic and persistence-free: every case here runs
/// against a policy value, with no Work, no editorial draft and no database.
/// </summary>
public sealed class GenreFormClassificationValidatorTests
{
    private const string Base = "http://id.loc.gov/authorities/genreForms/";

    private static readonly GenreFormClassificationValidator Validator = new();

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
                    Suggestion("gf2015026027", "Sustained defence of a position."),
                    Suggestion("gf2014026094", "Essay form throughout.")
                ]));

        Assert.True(validation.IsValid);
        Assert.Equal(2, validation.Result!.Suggested.Count);
    }

    [Fact]
    public void An_invented_term_is_rejected_and_never_coerced()
    {
        // The model returns a plausible label rather than an authority id.
        var validation = Validate(
            Raw(suggested: [Suggestion("Commentaries on the Psalms", "Looks apt.")]));

        Assert.False(validation.IsValid);
        Assert.Null(validation.Result);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.UnknownAuthorityTerm);
    }

    [Fact]
    public void A_structural_term_cannot_be_suggested()
    {
        // AC-MRA-02 and AC-MRA-05.
        var validation = Validate(
            Raw(suggested: [Suggestion("gf2015026044", "Religious in nature.")]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.TermNotSelectable);
    }

    [Fact]
    public void A_broader_term_alongside_its_descendant_is_rejected()
    {
        // Section 16: Hagiographies + Biographies is redundant hierarchy.
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("gf2015026032", "A saint's life."),
                    Suggestion("gf2014026049", "Also a life story.")
                ]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.RedundantHierarchy);
    }

    [Fact]
    public void Two_unrelated_terms_on_different_paths_are_not_redundant()
    {
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("gf2015026032", "A saint's life."),
                    Suggestion("gf2014026094", "Written as an essay.")
                ]));

        Assert.True(validation.IsValid);
    }

    [Fact]
    public void A_duplicated_term_is_rejected()
    {
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("gf2014026094", "Essay form."),
                    Suggestion("gf2014026094", "Essay form again.")
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
                suggested: [Suggestion("gf2014026094", "Essay form.")],
                rejected: [Rejection("gf2014026094", "Not really an essay.")]));

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
            Raw(suggested: [Suggestion("gf2014026094", "   ")]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.MissingJustification);
    }

    [Fact]
    public void A_rejection_without_reason_is_rejected()
    {
        var validation = Validate(
            Raw(rejected: [Rejection("gf2014026094", null)]));

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
                    Suggestion("gf2015026027", "One."),
                    Suggestion("gf2014026094", "Two."),
                    Suggestion("gf2015026032", "Three."),
                    Suggestion("gf2014026191", "Four."),
                    Suggestion("gf2015026051", "Five.")
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
                suggested: [Suggestion("gf2014026094", "Essay form.")],
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
    public void A_missing_identifier_is_rejected()
    {
        var validation = Validate(
            Raw(suggested: [Suggestion(null, "No identifier at all.")]));

        Assert.False(validation.IsValid);
        Assert.Contains(
            validation.Errors,
            x => x.Failure == GenreFormValidationFailure.MissingAuthorityId);
    }

    [Fact]
    public void A_single_violation_discards_the_whole_classification()
    {
        // Fail closed: one invented term makes the rest untrustworthy.
        var validation = Validate(
            Raw(
                suggested:
                [
                    Suggestion("gf2014026094", "Essay form."),
                    Suggestion("gf9999999999", "Invented.")
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
        Assert.Equal("apologia-genre-form-profile-v1", identity.PolicyVersion);
        Assert.Equal("genre-form-classification/1", identity.PromptVersion);
        Assert.Equal("ollama", identity.ModelProvider);
    }

    [Fact]
    public void An_authority_uri_resolves_as_well_as_an_identifier()
    {
        var validation = Validate(
            Raw(suggested: [Suggestion(Base + "gf2014026094", "Essay form.")]));

        Assert.True(validation.IsValid);
        Assert.Equal(
            "Essays",
            Assert.Single(validation.Result!.Suggested).PreferredLabel);
    }

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
        string? authorityId,
        string? justification)
    {
        return new RawGenreFormSuggestion(authorityId, justification, []);
    }

    private static RawGenreFormRejection Rejection(
        string? authorityId,
        string? reason)
    {
        return new RawGenreFormRejection(authorityId, reason);
    }

    private static MetadataReviewAnalysisIdentity Identity()
    {
        return new MetadataReviewAnalysisIdentity(
            "apologia-genre-form-profile-v1",
            "genre-form-classification/1",
            "ollama",
            "qwen3.6:27b",
            new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// A policy value mirroring the real profile: selectable terms plus the
    /// structural ancestors needed to detect redundant hierarchy.
    /// </summary>
    private static GenreFormPolicySnapshot Policy()
    {
        var religiousMaterials = Base + "gf2015026044";
        var informational = Base + "gf2014026114";
        var creativeNonfiction = Base + "gf2014026077";
        var biographies = Base + "gf2014026049";

        return new GenreFormPolicySnapshot(
            "apologia-genre-form-profile-v1",
            [
                Structural("gf2015026044", "Religious materials"),
                Structural("gf2014026114", "Informational works"),
                Structural("gf2014026077", "Creative nonfiction"),
                Selectable(
                    "gf2015026027",
                    "Apologetic writings",
                    [informational, religiousMaterials]),
                Selectable("gf2014026191", "Textbooks", []),
                Selectable("gf2015026051", "Sermons", [religiousMaterials]),
                Selectable(
                    "gf2014026049",
                    "Biographies",
                    [creativeNonfiction, informational]),
                Selectable(
                    "gf2015026032",
                    "Hagiographies",
                    [biographies, religiousMaterials, creativeNonfiction, informational]),
                Selectable(
                    "gf2014026094",
                    "Essays",
                    [creativeNonfiction, informational])
            ]);
    }

    private static GenreFormPolicyTerm Selectable(
        string identifier,
        string label,
        IReadOnlyList<string> ancestors)
    {
        return new GenreFormPolicyTerm(
            Base + identifier,
            identifier,
            label,
            GenreFormPolicyUsage.Selectable,
            ancestors);
    }

    private static GenreFormPolicyTerm Structural(string identifier, string label)
    {
        return new GenreFormPolicyTerm(
            Base + identifier,
            identifier,
            label,
            GenreFormPolicyUsage.StructuralOnly,
            []);
    }
}
