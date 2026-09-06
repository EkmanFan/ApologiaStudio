using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

/// <summary>
/// The canonical V1 product taxonomy.
/// </summary>
/// <remarks>
/// These assert the shape the product declares, not an implementation detail:
/// the counts, the closed vocabulary, and the fact that identity survives a
/// change of taxonomy version. A drift in any of them is a product change and
/// must be a deliberate one.
/// </remarks>
public sealed class ApologiaGenreFormTaxonomyTests
{
    #region Methods Shape

    [Fact]
    public void The_taxonomy_declares_twenty_seven_terms()
    {
        Assert.Equal(27, ApologiaGenreFormTaxonomy.Terms.Count);
    }

    [Fact]
    public void Twenty_four_terms_are_encoder_predictable()
    {
        Assert.Equal(24, ApologiaGenreFormTaxonomy.EncoderPredictableTerms.Count());
    }

    [Fact]
    public void Three_terms_are_manual_only()
    {
        var manual = ApologiaGenreFormTaxonomy.ManualOnlyTerms
            .Select(x => x.Code)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["instructional_lesson", "study_guide", "training_material"],
            manual);
    }

    [Fact]
    public void Codes_labels_definitions_and_order_are_present_and_unique()
    {
        var terms = ApologiaGenreFormTaxonomy.Terms;

        Assert.All(
            terms,
            term =>
            {
                Assert.False(string.IsNullOrWhiteSpace(term.Code));
                Assert.False(string.IsNullOrWhiteSpace(term.PreferredLabel));
                Assert.False(string.IsNullOrWhiteSpace(term.Definition));
            });

        Assert.Equal(
            terms.Count,
            terms.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            terms.Count,
            terms.Select(x => x.DisplayOrder).Distinct().Count());
    }

    [Fact]
    public void The_expected_encoder_scope_is_exactly_the_v2_1_scope()
    {
        // The encoder scope is a product decision, not a model artefact: it must
        // not drift because a later model was trained on a different head set.
        Assert.Equal(
            [
                "academic_degree_work", "anthology", "apologetic_writing",
                "autobiography", "biography", "catechism", "collected_works",
                "commentary", "conference_proceedings", "correspondence",
                "creed", "devotional_literature", "diary", "dictionary",
                "edited_volume", "encyclopedia", "essays", "handbook_manual",
                "personal_narrative", "prayer", "sacred_work",
                "scholarly_article", "sermon", "textbook"
            ],
            ApologiaGenreFormTaxonomy.EncoderPredictableTerms
                .Select(x => x.Code)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void The_taxonomy_is_flat()
    {
        // V1 declares no product hierarchy. The LCGFT broader relations stay in
        // the external authority catalogue, where they describe that authority.
        Assert.DoesNotContain(
            typeof(ApologiaGenreFormTerm).GetProperties(),
            property =>
                property.Name.Contains("Parent", StringComparison.Ordinal) ||
                property.Name.Contains("Broader", StringComparison.Ordinal) ||
                property.Name.Contains("Narrower", StringComparison.Ordinal));
    }

    #endregion

    #region Methods Identity

    [Fact]
    public void Identity_is_stable_across_calls()
    {
        Assert.Equal(
            ApologiaGenreFormTaxonomy.StableId("apologetic_writing"),
            ApologiaGenreFormTaxonomy.StableId("apologetic_writing"));
    }

    [Fact]
    public void Identity_does_not_depend_on_the_taxonomy_version()
    {
        // A concept whose meaning does not change must keep its identity when
        // the taxonomy version moves, so an assignment made under V1 still
        // points at the same concept under V2.
        var derived = ApologiaGenreFormTaxonomy.StableId("creed");

        Assert.DoesNotContain(
            ApologiaGenreFormTaxonomy.Version,
            derived.ToString());
        Assert.Equal(
            derived,
            ApologiaGenreFormTaxonomy.Find("creed")?.Id);
    }

    [Fact]
    public void Distinct_codes_yield_distinct_identities()
    {
        Assert.Equal(
            ApologiaGenreFormTaxonomy.Terms.Count,
            ApologiaGenreFormTaxonomy.Terms.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void An_empty_code_has_no_identity()
    {
        Assert.Throws<ArgumentException>(
            () => ApologiaGenreFormTaxonomy.StableId(" "));
    }

    [Fact]
    public void A_term_is_found_by_its_code_and_never_by_its_label()
    {
        Assert.NotNull(ApologiaGenreFormTaxonomy.Find("sacred_work"));
        Assert.Null(ApologiaGenreFormTaxonomy.Find("Sacred work"));
        Assert.Null(ApologiaGenreFormTaxonomy.Find(null));
    }

    #endregion

    #region Methods Definitions

    [Fact]
    public void Creed_carries_the_broad_product_definition()
    {
        var creed = ApologiaGenreFormTaxonomy.Find("creed");

        Assert.Contains(
            "religious or non-religious",
            creed!.Definition,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "not necessarily itself a creed",
            creed.Definition,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_version_is_the_declared_v1_identifier()
    {
        Assert.Equal(
            "apologia-genre-form-v1",
            ApologiaGenreFormTaxonomy.Version);
    }

    #endregion
}
