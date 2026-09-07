using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.MetadataReview;

/// <summary>
/// Projects the canonical Apologia Genre/Form taxonomy into a policy value.
/// </summary>
/// <remarks>
/// Reads the product taxonomy and nothing else. The external authority
/// catalogue, its hierarchy and its profile are not consulted: what a reviewer
/// or an assistant may choose is a product decision, and making it depend on an
/// LCGFT import would put the product's own vocabulary at the mercy of a
/// maintenance operation on someone else's thesaurus.
///
/// Cardinality is never assumed: the policy is whatever the taxonomy currently
/// holds as active.
/// </remarks>
public sealed class KnowledgeStoreGenreFormPolicyProvider(
    KnowledgeDbContext context)
    : IGenreFormPolicyProvider
{
    public async Task<GenreFormPolicySnapshot> GetActivePolicyAsync(
        CancellationToken cancellationToken)
    {
        var rows = await context.ApologiaGenreFormTerms
            .AsNoTracking()
            .Where(x => x.TaxonomyVersion == ApologiaGenreFormTaxonomy.Version &&
                        x.Status == "active")
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new
            {
                x.Code,
                x.PreferredLabel,
                x.PredictionMode
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new GenreFormAuthorityException(
                "No active Genre/Form taxonomy is present; seed the canonical " +
                "taxonomy before requesting a classification policy.");
        }

        var terms = rows
            .Select(x => new GenreFormPolicyTerm(
                x.Code,
                x.PreferredLabel,
                x.PredictionMode == "manual_only"
                    ? GenreFormPredictionMode.ManualOnly
                    : GenreFormPredictionMode.EncoderPredictable))
            .ToList();

        return new GenreFormPolicySnapshot(
            ApologiaGenreFormTaxonomy.Version,
            terms);
    }
}
