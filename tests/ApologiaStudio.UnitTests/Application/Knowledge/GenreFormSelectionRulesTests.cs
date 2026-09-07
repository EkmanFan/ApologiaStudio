using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

/// <summary>
/// The Genre/Form selection rules, after the product cutover.
/// </summary>
/// <remarks>
/// A reviewer's hand-made selection and an assistant's proposal go through
/// these same rules. What they assert is deliberately small: on a flat, closed
/// taxonomy there is nothing else to check, and a rule kept for its own sake
/// would reject legitimate selections.
/// </remarks>
public sealed class GenreFormSelectionRulesTests
{
    #region Methods Vocabulary

    [Fact]
    public void Every_active_product_term_is_a_valid_selection()
    {
        var codes = ApologiaGenreFormTaxonomy.Terms
            .Select(x => x.Code)
            .ToList();

        Assert.Equal(27, codes.Count);
        Assert.Empty(GenreFormSelectionRules.Validate(codes, Policy()));
    }

    [Fact]
    public void A_manual_only_term_is_selectable_by_a_reviewer()
    {
        // Prediction mode bounds the future encoder scope, never human choice.
        Assert.Empty(
            GenreFormSelectionRules.Validate(
                ["study_guide", "training_material", "instructional_lesson"],
                Policy()));
    }

    [Fact]
    public void An_empty_selection_is_valid()
    {
        Assert.Empty(GenreFormSelectionRules.Validate([], Policy()));
    }

    [Fact]
    public void Several_terms_are_valid_together()
    {
        Assert.Empty(
            GenreFormSelectionRules.Validate(
                ["sermon", "commentary", "biography", "prayer"],
                Policy()));
    }

    [Fact]
    public void An_unknown_term_is_refused()
    {
        var errors = GenreFormSelectionRules.Validate(
            ["not_a_genre"],
            Policy());

        Assert.Equal(
            GenreFormSelectionFailure.UnknownTerm,
            Assert.Single(errors).Failure);
    }

    [Fact]
    public void An_lcgft_identity_is_not_a_selectable_product_term()
    {
        var errors = GenreFormSelectionRules.Validate(
            [
                "gf2015026051",
                "http://id.loc.gov/authorities/genreForms/gf2015026051",
                "Sermon"
            ],
            Policy());

        Assert.Equal(3, errors.Count);
        Assert.All(
            errors,
            x => Assert.Equal(GenreFormSelectionFailure.UnknownTerm, x.Failure));
    }

    [Fact]
    public void A_duplicated_term_is_refused()
    {
        var errors = GenreFormSelectionRules.Validate(
            ["sermon", "sermon"],
            Policy());

        Assert.Equal(
            GenreFormSelectionFailure.Duplicate,
            Assert.Single(errors).Failure);
    }

    [Fact]
    public void No_pair_of_product_terms_is_ever_redundant()
    {
        // The taxonomy is flat: there is no ancestor path, so every pair of
        // distinct terms stands. Under the former LCGFT profile several of
        // these pairs were hierarchy violations.
        foreach (var first in ApologiaGenreFormTaxonomy.Terms)
        {
            foreach (var second in ApologiaGenreFormTaxonomy.Terms)
            {
                if (first.Code == second.Code)
                {
                    continue;
                }

                Assert.Empty(
                    GenreFormSelectionRules.Validate(
                        [first.Code, second.Code],
                        Policy()));
            }
        }
    }

    [Fact]
    public void The_rules_declare_only_the_two_product_failures()
    {
        // A regression here would mean a structural or hierarchical notion was
        // reintroduced without a taxonomy that could justify it.
        Assert.Equal(
            [
                GenreFormSelectionFailure.UnknownTerm,
                GenreFormSelectionFailure.Duplicate
            ],
            Enum.GetValues<GenreFormSelectionFailure>());
    }

    #endregion

    #region Methods Reviewer Outcome

    [Fact]
    public void Confirming_the_proposal_unchanged_is_accepted()
    {
        Assert.Equal(
            MetadataReviewOutcome.Accepted,
            MetadataReviewOutcomeCalculator.Determine(
                ["apologetic_writing", "essays"],
                ["essays", "apologetic_writing"]));
    }

    [Fact]
    public void Agreeing_that_nothing_applies_is_accepted()
    {
        Assert.Equal(
            MetadataReviewOutcome.Accepted,
            MetadataReviewOutcomeCalculator.Determine([], []));
    }

    [Fact]
    public void Keeping_part_of_the_proposal_is_modified()
    {
        Assert.Equal(
            MetadataReviewOutcome.Modified,
            MetadataReviewOutcomeCalculator.Determine(
                ["apologetic_writing", "essays"],
                ["apologetic_writing", "sermon"]));
    }

    [Fact]
    public void Keeping_none_of_the_proposal_is_rejected()
    {
        Assert.Equal(
            MetadataReviewOutcome.Rejected,
            MetadataReviewOutcomeCalculator.Determine(
                ["apologetic_writing"],
                ["sermon"]));

        Assert.Equal(
            MetadataReviewOutcome.Rejected,
            MetadataReviewOutcomeCalculator.Determine(
                ["apologetic_writing"],
                []));
    }

    #endregion

    #region Methods Helpers

    private static GenreFormPolicySnapshot Policy() =>
        new(
            ApologiaGenreFormTaxonomy.Version,
            ApologiaGenreFormTaxonomy.Terms
                .Select(x => new GenreFormPolicyTerm(
                    x.Code,
                    x.PreferredLabel,
                    x.PredictionMode))
                .ToList());

    #endregion
}
