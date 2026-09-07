using System.Reflection;
using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.ArchitectureTests;

/// <summary>
/// The boundary between the Apologia product taxonomy and the external LCGFT
/// catalogue, after GF-TAX-6.
/// </summary>
/// <remarks>
/// The product used to carry a second Genre/Form vocabulary — an LCGFT profile
/// of fourteen selectable and eight structural terms. It is gone. These assert
/// that it has not come back under another name, and that what remains of the
/// external catalogue really is only an authority.
/// </remarks>
public sealed class GenreFormProductBoundaryTests
{
    #region Variables and Constants

    private static readonly Assembly Application =
        typeof(ApologiaGenreFormTaxonomy).Assembly;

    private static readonly Assembly Infrastructure =
        typeof(ApologiaStudio.Infrastructure.DependencyInjection).Assembly;

    #endregion

    #region Methods

    [Fact]
    public void The_old_product_profile_no_longer_exists()
    {
        foreach (var name in new[]
                 {
                     "GenreFormProfile",
                     "IGenreFormProfileSeeder",
                     "PostgreSqlGenreFormProfileSeeder",
                     "GenreFormProfileSeedResult",
                     "GenreFormProfileEntryEntity",
                     "GenreFormUsageStatus",
                     "GenreFormPolicyUsage"
                 })
        {
            Assert.DoesNotContain(
                Application.GetTypes().Concat(Infrastructure.GetTypes()),
                type => string.Equals(type.Name, name, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_authority_store_exposes_no_product_usage_query()
    {
        var members = typeof(IGenreFormAuthorityStore)
            .GetMethods()
            .Select(x => x.Name)
            .ToList();

        // Import and catalogue reads remain; the profile projection is gone.
        Assert.Equal(
            [
                "GetBroaderTermsAsync",
                "GetNarrowerTermsAsync",
                "GetTermByAuthorityUriAsync",
                "ImportAsync"
            ],
            members.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void An_authority_term_view_carries_authority_facts_only()
    {
        var properties = typeof(GenreFormTermView)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("UsageStatus", properties);
        Assert.DoesNotContain("DisplayOrder", properties);
    }

    [Fact]
    public void The_selection_policy_declares_only_product_concepts()
    {
        var properties = typeof(GenreFormPolicyTerm)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.Equal(
            ["Code", "PreferredLabel", "PredictionMode"],
            properties);
    }

    [Fact]
    public void The_refresh_report_names_the_aligned_product_terms()
    {
        // The local dependency on an external concept is an alignment. The
        // report must say which product concept is at risk, not restate a
        // profile status that no longer exists.
        var properties = typeof(GenreFormAuthorityReviewItem)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.Contains("AlignedProductTermCodes", properties);
        Assert.DoesNotContain("UsageStatus", properties);
    }

    #endregion
}
