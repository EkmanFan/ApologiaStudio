using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.MetadataReview;

/// <summary>
/// Projects the active Genre/Form profile into a policy value.
///
/// Reads the existing profile and authority tables only; this phase introduces
/// no persistence of its own. Cardinality is never assumed: the selectable set
/// is whatever the active profile currently contains.
/// </summary>
public sealed class KnowledgeStoreGenreFormPolicyProvider(
    KnowledgeDbContext context)
    : IGenreFormPolicyProvider
{
    public async Task<GenreFormPolicySnapshot> GetActivePolicyAsync(
        CancellationToken cancellationToken)
    {
        var entries = await (
            from entry in context.GenreFormProfileEntries.AsNoTracking()
            join term in context.GenreFormTerms.AsNoTracking()
                on entry.TermId equals term.Id
            where entry.ProfileVersion == GenreFormProfile.Version &&
                  entry.UsageStatus != "excluded"
            select new
            {
                term.Id,
                term.AuthorityUri,
                term.AuthorityIdentifier,
                term.PreferredLabel,
                entry.UsageStatus,
                entry.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            throw new GenreFormAuthorityException(
                "No active Genre/Form profile is present; apply the profile " +
                "before requesting a classification policy.");
        }

        var ids = entries.Select(x => x.Id).ToList();

        var relations = await context.GenreFormBroaderRelations
            .AsNoTracking()
            .Select(x => new { x.NarrowerTermId, x.BroaderTermId })
            .ToListAsync(cancellationToken);

        var uriById = await context.GenreFormTerms
            .AsNoTracking()
            .Select(x => new { x.Id, x.AuthorityUri })
            .ToDictionaryAsync(x => x.Id, x => x.AuthorityUri, cancellationToken);

        var parents = relations
            .GroupBy(x => x.NarrowerTermId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(r => r.BroaderTermId).ToList());

        var terms = entries
            .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
            .ThenBy(x => x.PreferredLabel, StringComparer.Ordinal)
            .Select(x => new GenreFormPolicyTerm(
                x.AuthorityUri,
                x.AuthorityIdentifier,
                x.PreferredLabel,
                x.UsageStatus == "selectable"
                    ? GenreFormPolicyUsage.Selectable
                    : GenreFormPolicyUsage.StructuralOnly,
                Ancestors(x.Id, parents, uriById)))
            .ToList();

        return new GenreFormPolicySnapshot(GenreFormProfile.Version, terms);
    }

    /// <summary>
    /// Transitive ancestors, so hierarchy redundancy can be detected without a
    /// further query during validation.
    /// </summary>
    private static IReadOnlyList<string> Ancestors(
        Guid termId,
        IReadOnlyDictionary<Guid, List<Guid>> parents,
        IReadOnlyDictionary<Guid, string> uriById)
    {
        var seen = new HashSet<Guid>();
        var frontier = new List<Guid> { termId };

        while (frontier.Count > 0)
        {
            var next = new List<Guid>();

            foreach (var current in frontier)
            {
                if (!parents.TryGetValue(current, out var candidates))
                {
                    continue;
                }

                next.AddRange(candidates.Where(seen.Add));
            }

            frontier = next;
        }

        return seen
            .Where(uriById.ContainsKey)
            .Select(x => uriById[x])
            .ToList();
    }
}
