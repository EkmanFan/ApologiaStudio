using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

/// <summary>
/// The approved external alignments of the product taxonomy.
/// </summary>
/// <remarks>
/// These lock the approved set and, above all, the direction in which a
/// mapping kind is read. Everything downstream — validation, suggestion,
/// export — will infer from that direction, and a silent reversal produces
/// answers that look reasonable and are backwards.
/// </remarks>
public sealed class ApologiaGenreFormAuthorityMappingsTests
{
    #region Methods Direction

    [Fact]
    public void Broader_means_the_Apologia_concept_is_wider_than_the_external_one()
    {
        // The convention is: Apologia product term → external authority
        // concept. Both Broader cases are wider on the Apologia side, and
        // neither is ever recorded as Narrower.
        //
        // creed covers any explicit profession of belief, religious or not;
        // LCGFT Creeds is confined to religious ones.
        //
        // academic_degree_work covers any degree-qualifying academic work;
        // LCGFT Academic theses is confined to theses.
        var broader = ApologiaGenreFormAuthorityMappings.Approved
            .Where(x => x.MappingKind == GenreFormMappingKind.Broader)
            .Select(x => x.ProductTermCode)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["academic_degree_work", "creed"], broader);

        Assert.DoesNotContain(
            ApologiaGenreFormAuthorityMappings.Approved,
            x => x.MappingKind == GenreFormMappingKind.Narrower);
    }

    [Fact]
    public void The_mapping_kind_values_are_fixed()
    {
        // The persisted form is derived from these members. Renumbering or
        // reordering them would re-label existing rows without any migration
        // saying so.
        Assert.Equal(0, (int)GenreFormMappingKind.Exact);
        Assert.Equal(1, (int)GenreFormMappingKind.Broader);
        Assert.Equal(2, (int)GenreFormMappingKind.Narrower);
        Assert.Equal(3, (int)GenreFormMappingKind.Close);
        Assert.Equal(4, (int)GenreFormMappingKind.Related);
    }

    #endregion

    #region Methods Shape

    [Fact]
    public void Twelve_lcgft_alignments_are_approved()
    {
        Assert.Equal(12, ApologiaGenreFormAuthorityMappings.Approved.Count);
        Assert.All(
            ApologiaGenreFormAuthorityMappings.Approved,
            x => Assert.Equal(GenreFormAuthority.Lcgft, x.Authority));
    }

    [Fact]
    public void Ten_are_exact_and_two_are_broader()
    {
        var byKind = ApologiaGenreFormAuthorityMappings.Approved
            .GroupBy(x => x.MappingKind)
            .ToDictionary(x => x.Key, x => x.Count());

        Assert.Equal(10, byKind[GenreFormMappingKind.Exact]);
        Assert.Equal(2, byKind[GenreFormMappingKind.Broader]);
        Assert.Equal(2, byKind.Count);
    }

    [Fact]
    public void The_approved_alignments_are_exactly_the_declared_pairs()
    {
        Assert.Equal(
            [
                "academic_degree_work → gf2014026039 (broader)",
                "apologetic_writing → gf2015026027 (exact)",
                "biography → gf2014026049 (exact)",
                "catechism → gf2015026029 (exact)",
                "commentary → gf2025026014 (exact)",
                "creed → gf2015026031 (broader)",
                "devotional_literature → gf2015026057 (exact)",
                "essays → gf2014026094 (exact)",
                "prayer → gf2015026049 (exact)",
                "sacred_work → gf2015026045 (exact)",
                "sermon → gf2015026051 (exact)",
                "textbook → gf2014026191 (exact)"
            ],
            ApologiaGenreFormAuthorityMappings.Approved
                .Select(x =>
                    $"{x.ProductTermCode} → {x.ExternalConceptId} " +
                    $"({x.MappingKind.ToString().ToLowerInvariant()})")
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void Every_aligned_product_term_exists_in_the_canonical_taxonomy()
    {
        Assert.All(
            ApologiaGenreFormAuthorityMappings.Approved,
            x => Assert.NotNull(
                ApologiaGenreFormTaxonomy.Find(x.ProductTermCode)));
    }

    [Fact]
    public void Fifteen_product_terms_have_no_approved_alignment()
    {
        // An absent mapping is a valid state. Filling the gap to make the
        // table look complete would assert an equivalence nobody approved.
        var aligned = ApologiaGenreFormAuthorityMappings.Approved
            .Select(x => x.ProductTermCode)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            15,
            ApologiaGenreFormTaxonomy.Terms.Count(
                x => !aligned.Contains(x.Code)));
    }

    #endregion

    #region Methods Exclusions

    [Fact]
    public void Pastoral_letters_and_hagiographies_are_deliberately_unmapped()
    {
        Assert.DoesNotContain(
            ApologiaGenreFormAuthorityMappings.Approved,
            x => x.ExternalConceptId is "gf2015026047" or "gf2015026032");
    }

    [Fact]
    public void No_bnf_alignment_is_declared()
    {
        // The model accepts BnF; the repository holds no authoritative BnF
        // alignment, and none is reconstructed from memory.
        Assert.Empty(
            ApologiaGenreFormAuthorityMappings.ForAuthority(
                GenreFormAuthority.Bnf));
    }

    #endregion

    #region Methods Identity

    [Fact]
    public void Identities_and_pairs_are_unique()
    {
        var approved = ApologiaGenreFormAuthorityMappings.Approved;

        Assert.Equal(
            approved.Count,
            approved.Select(x => x.Id).Distinct().Count());
        Assert.Equal(
            approved.Count,
            approved
                .Select(x => (x.ProductTermCode, x.ExternalConceptId))
                .Distinct()
                .Count());
    }

    [Fact]
    public void Identity_is_stable_and_derived_from_the_whole_alignment()
    {
        Assert.Equal(
            ApologiaGenreFormAuthorityMappings.StableId(
                "lcgft",
                "creed",
                "gf2015026031"),
            ApologiaGenreFormAuthorityMappings.StableId(
                "lcgft",
                "creed",
                "gf2015026031"));

        Assert.NotEqual(
            ApologiaGenreFormAuthorityMappings.StableId(
                "lcgft",
                "creed",
                "gf2015026031"),
            ApologiaGenreFormAuthorityMappings.StableId(
                "bnf",
                "creed",
                "gf2015026031"));
        Assert.NotEqual(
            ApologiaGenreFormAuthorityMappings.StableId(
                "lcgft",
                "creed",
                "gf2015026031"),
            ApologiaGenreFormAuthorityMappings.StableId(
                "lcgft",
                "creed",
                "gf2015026032"));
    }

    [Fact]
    public void An_incomplete_alignment_has_no_identity()
    {
        Assert.Throws<ArgumentException>(
            () => ApologiaGenreFormAuthorityMappings.StableId(
                "lcgft",
                " ",
                "gf2015026031"));
    }

    #endregion

    #region Methods Declaration

    [Fact]
    public void Every_lcgft_alignment_carries_the_canonical_concept_uri()
    {
        Assert.All(
            ApologiaGenreFormAuthorityMappings.ForAuthority(
                GenreFormAuthority.Lcgft),
            x => Assert.Equal(
                "http://id.loc.gov/authorities/genreForms/" +
                x.ExternalConceptId,
                x.ExternalConceptUri));
    }

    #endregion
}
