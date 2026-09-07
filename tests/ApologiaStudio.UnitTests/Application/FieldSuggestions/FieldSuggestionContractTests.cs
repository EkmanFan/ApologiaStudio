using ApologiaStudio.Application.Abstractions.FieldSuggestions;

namespace ApologiaStudio.UnitTests.Application.FieldSuggestions;

/// <summary>
/// The field-suggestion contracts.
/// </summary>
/// <remarks>
/// A provider is untrusted by construction, so the invariants live in the types
/// themselves rather than in each caller's good manners. These assert exactly
/// what a provider cannot get away with.
/// </remarks>
public sealed class FieldSuggestionContractTests
{
    #region Variables and Constants

    private static readonly FieldSuggestionProvenance Provenance = new("test");

    #endregion

    #region Methods Field Identity

    [Fact]
    public void The_genre_form_field_is_declared()
    {
        Assert.Equal("genre_form", ApologiaFieldId.GenreForm.Value);
        Assert.Equal("genre_form", ApologiaFieldId.GenreFormValue);
        Assert.Equal(
            ApologiaFieldId.GenreForm,
            new ApologiaFieldId("genre_form"));
    }

    [Fact]
    public void A_new_field_needs_no_change_to_the_contract()
    {
        // The identifier is extensible on purpose: a future assisted field is a
        // new value, not a new interface.
        var field = new ApologiaFieldId("publication_year");

        Assert.Equal("publication_year", field.Value);
        Assert.NotEqual(ApologiaFieldId.GenreForm, field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Genre_Form")]
    [InlineData("GENRE_FORM")]
    [InlineData(" genre_form ")]
    [InlineData("genre form")]
    [InlineData("genre-form")]
    [InlineData("_genre_form")]
    [InlineData("genre__form")]
    [InlineData("genre_form_")]
    [InlineData("1genre")]
    public void A_non_canonical_field_is_refused_and_never_normalised(string value)
    {
        // Silently folding a near-miss would let a caller believe it addressed
        // a field it did not.
        Assert.Throws<ArgumentException>(() => new ApologiaFieldId(value));
    }

    [Fact]
    public void A_null_field_value_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new ApologiaFieldId(null!));
    }

    #endregion

    #region Methods Suggestion

    [Theory]
    [InlineData(0d)]
    [InlineData(0.47d)]
    [InlineData(1d)]
    public void A_score_inside_the_accepted_range_is_valid(double score)
    {
        Assert.Equal(score, new FieldSuggestion("sermon", score).Score);
    }

    [Theory]
    [InlineData(-0.01d)]
    [InlineData(1.01d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_score_outside_the_accepted_range_is_refused(double score)
    {
        Assert.Throws<FieldSuggestionContractException>(
            () => new FieldSuggestion("sermon", score));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" sermon")]
    [InlineData("sermon ")]
    public void An_unusable_value_identity_is_refused(string valueId)
    {
        Assert.Throws<FieldSuggestionContractException>(
            () => new FieldSuggestion(valueId, 0.5));
    }

    #endregion

    #region Methods Status Consistency

    [Fact]
    public void Success_without_a_suggestion_is_refused()
    {
        var exception = Assert.Throws<FieldSuggestionContractException>(
            () => FieldSuggestionResult.Succeeded(
                ApologiaFieldId.GenreForm,
                [],
                Provenance));

        Assert.Contains(
            "NoSuggestion",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FieldSuggestionStatus.NoSuggestion)]
    [InlineData(FieldSuggestionStatus.Unavailable)]
    [InlineData(FieldSuggestionStatus.Failed)]
    public void A_non_successful_status_carrying_suggestions_is_refused(
        FieldSuggestionStatus status)
    {
        Assert.Throws<FieldSuggestionContractException>(
            () => new FieldSuggestionResult(
                ApologiaFieldId.GenreForm,
                status,
                [new FieldSuggestion("sermon", 0.9)],
                Provenance));
    }

    [Fact]
    public void The_three_negatives_stay_distinct()
    {
        // Ran and found nothing / never ran / broke. Collapsing any two would
        // turn an outage into an opinion.
        var noSuggestion = FieldSuggestionResult.NoSuggestion(
            ApologiaFieldId.GenreForm,
            Provenance);
        var unavailable = FieldSuggestionResult.Unavailable(
            ApologiaFieldId.GenreForm,
            Provenance);
        var failed = FieldSuggestionResult.Failed(
            ApologiaFieldId.GenreForm,
            Provenance);

        Assert.Equal(FieldSuggestionStatus.NoSuggestion, noSuggestion.Status);
        Assert.Equal(FieldSuggestionStatus.Unavailable, unavailable.Status);
        Assert.Equal(FieldSuggestionStatus.Failed, failed.Status);

        Assert.Equal(
            3,
            new[] { noSuggestion.Status, unavailable.Status, failed.Status }
                .Distinct()
                .Count());
    }

    [Fact]
    public void A_successful_result_carries_its_suggestions()
    {
        var result = FieldSuggestionResult.Succeeded(
            ApologiaFieldId.GenreForm,
            [
                new FieldSuggestion("sermon", 0.91),
                new FieldSuggestion("commentary", 0.62)
            ],
            Provenance);

        Assert.Equal(FieldSuggestionStatus.Succeeded, result.Status);
        Assert.Equal(["sermon", "commentary"], result.Suggestions.Select(x => x.ValueId));
    }

    [Fact]
    public void A_duplicated_value_is_refused()
    {
        Assert.Throws<FieldSuggestionContractException>(
            () => FieldSuggestionResult.Succeeded(
                ApologiaFieldId.GenreForm,
                [
                    new FieldSuggestion("sermon", 0.9),
                    new FieldSuggestion("sermon", 0.4)
                ],
                Provenance));
    }

    [Fact]
    public void A_result_without_a_field_identity_is_refused()
    {
        Assert.Throws<FieldSuggestionContractException>(
            () => FieldSuggestionResult.Unavailable(default, Provenance));
    }

    [Fact]
    public void Provenance_is_required_and_names_its_producer()
    {
        Assert.Throws<ArgumentNullException>(
            () => FieldSuggestionResult.Unavailable(
                ApologiaFieldId.GenreForm,
                null!));
        Assert.Throws<ArgumentException>(
            () => new FieldSuggestionProvenance("  "));

        // Nothing but the producer is claimed until a real model plan fills it.
        var provenance = new FieldSuggestionProvenance("unavailable");
        Assert.Null(provenance.PlanId);
        Assert.Null(provenance.ModelId);
        Assert.Null(provenance.ModelVersion);
        Assert.Null(provenance.HeadId);
    }

    #endregion

    #region Methods Batch

    [Fact]
    public void A_field_is_answered_at_most_once()
    {
        Assert.Throws<FieldSuggestionContractException>(
            () => new FieldSuggestionBatch(
                [
                    FieldSuggestionResult.Unavailable(
                        ApologiaFieldId.GenreForm,
                        Provenance),
                    FieldSuggestionResult.NoSuggestion(
                        ApologiaFieldId.GenreForm,
                        Provenance)
                ]));
    }

    [Fact]
    public void An_unanswered_requested_field_is_refused()
    {
        var batch = new FieldSuggestionBatch(
            [
                FieldSuggestionResult.Unavailable(
                    ApologiaFieldId.GenreForm,
                    Provenance)
            ]);

        batch.EnsureAnswers([ApologiaFieldId.GenreForm]);
        batch.EnsureAnswers(null);

        var exception = Assert.Throws<FieldSuggestionContractException>(
            () => batch.EnsureAnswers(
                [ApologiaFieldId.GenreForm, new ApologiaFieldId("language")]));

        Assert.Contains("language", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_batch_finds_the_result_for_a_field()
    {
        var batch = new FieldSuggestionBatch(
            [
                FieldSuggestionResult.NoSuggestion(
                    ApologiaFieldId.GenreForm,
                    Provenance)
            ]);

        Assert.Equal(
            FieldSuggestionStatus.NoSuggestion,
            batch.Find(ApologiaFieldId.GenreForm)!.Status);
        Assert.Null(batch.Find(new ApologiaFieldId("language")));
        Assert.Empty(FieldSuggestionBatch.Empty.Results);
    }

    #endregion

    #region Methods Evidence

    [Fact]
    public void The_request_carries_only_what_apologia_actually_holds()
    {
        var evidence = new FieldSuggestionEvidence(
            "A Defence of the Faith",
            "An apologetic treatise.");

        var request = new FieldSuggestionRequest(Guid.NewGuid(), evidence);

        Assert.False(request.Evidence.IsEmpty);
        Assert.True(FieldSuggestionEvidence.Empty.IsEmpty);
        Assert.True(new FieldSuggestionEvidence(null, "   ").IsEmpty);
    }

    #endregion
}
