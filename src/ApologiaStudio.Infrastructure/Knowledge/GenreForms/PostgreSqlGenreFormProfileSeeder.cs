using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.GenreForms;

/// <summary>
/// Applies Apologia Genre/Form Profile V1 over the imported LCGFT authority.
///
/// Approved terms are resolved by preferred label; the required structural
/// ancestors are derived from the imported hierarchy rather than declared, so
/// the profile always reflects the authority actually present.
/// </summary>
public sealed class PostgreSqlGenreFormProfileSeeder(
    KnowledgeDbContext context)
    : IGenreFormProfileSeeder
{
    public async Task<GenreFormProfileSeedResult> ApplyAsync(
        CancellationToken cancellationToken)
    {
        var selectableIds = await ResolveSelectableAsync(cancellationToken);
        var structural = await DeriveStructuralAncestorsAsync(
            selectableIds.Values.ToList(),
            cancellationToken);

        var desired = new Dictionary<Guid, (string Usage, int? Order)>();

        var order = 0;
        foreach (var label in GenreFormProfile.SelectableLabels)
        {
            order++;
            desired[selectableIds[label]] = ("selectable", order);
        }

        foreach (var termId in structural.Keys)
        {
            // A term approved for assignment stays selectable even when it is
            // also an ancestor of another approved term.
            if (!desired.ContainsKey(termId))
            {
                desired[termId] = ("structural_only", null);
            }
        }

        var structuralLabels = structural
            .Where(x => !selectableIds.Values.Contains(x.Key))
            .Select(x => x.Value)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // Counted before applying: ApplyEntriesAsync consumes the dictionary.
        var structuralCount = desired.Count(x => x.Value.Usage == "structural_only");

        var changed = await ApplyEntriesAsync(desired, cancellationToken);

        return new GenreFormProfileSeedResult(
            GenreFormProfile.Version,
            GenreFormProfile.SelectableLabels.Count,
            structuralCount,
            structuralLabels,
            changed);
    }

    private async Task<Dictionary<string, Guid>> ResolveSelectableAsync(
        CancellationToken cancellationToken)
    {
        var labels = GenreFormProfile.SelectableLabels;

        var candidates = await context.GenreFormTerms
            .AsNoTracking()
            .Where(x => labels.Contains(x.PreferredLabel))
            .Select(x => new { x.Id, x.PreferredLabel })
            .ToListAsync(cancellationToken);

        var resolved = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var label in labels)
        {
            var matches = candidates
                .Where(x => string.Equals(x.PreferredLabel, label, StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
            {
                // Fail closed: an approved term that the authority does not
                // publish must never be invented locally.
                throw new GenreFormAuthorityException(
                    $"Approved profile term '{label}' is absent from the " +
                    "imported authority snapshot.");
            }

            if (matches.Count > 1)
            {
                throw new GenreFormAuthorityException(
                    $"Approved profile term '{label}' is ambiguous: " +
                    $"{matches.Count} authority terms share that label.");
            }

            resolved[label] = matches[0].Id;
        }

        return resolved;
    }

    /// <summary>
    /// Full transitive broader closure: every ancestor of an approved term is
    /// structural unless it is itself approved. Deterministic, and independent
    /// of how deep the thesaurus happens to be for a given term.
    /// </summary>
    private async Task<Dictionary<Guid, string>> DeriveStructuralAncestorsAsync(
        IReadOnlyList<Guid> selectableIds,
        CancellationToken cancellationToken)
    {
        var ancestorIds = new HashSet<Guid>();
        var frontier = selectableIds.ToList();

        while (frontier.Count > 0)
        {
            var parents = await context.GenreFormBroaderRelations
                .AsNoTracking()
                .Where(x => frontier.Contains(x.NarrowerTermId))
                .Select(x => x.BroaderTermId)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Adding returns false for an ancestor already seen, which also
            // terminates the walk if the authority ever contains a cycle.
            frontier = parents.Where(ancestorIds.Add).ToList();
        }

        var ancestors = await context.GenreFormTerms
            .AsNoTracking()
            .Where(x => ancestorIds.Contains(x.Id))
            .Select(x => new { x.Id, x.PreferredLabel })
            .ToListAsync(cancellationToken);

        return ancestors.ToDictionary(x => x.Id, x => x.PreferredLabel);
    }

    private async Task<bool> ApplyEntriesAsync(
        Dictionary<Guid, (string Usage, int? Order)> desired,
        CancellationToken cancellationToken)
    {
        var existing = await context.GenreFormProfileEntries
            .Where(x => x.ProfileVersion == GenreFormProfile.Version)
            .ToListAsync(cancellationToken);

        var changed = false;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in existing)
        {
            if (!desired.TryGetValue(entry.TermId, out var wanted))
            {
                context.GenreFormProfileEntries.Remove(entry);
                changed = true;
                continue;
            }

            if (entry.UsageStatus != wanted.Usage ||
                entry.DisplayOrder != wanted.Order)
            {
                entry.UsageStatus = wanted.Usage;
                entry.DisplayOrder = wanted.Order;
                entry.UpdatedAt = now;
                changed = true;
            }

            desired.Remove(entry.TermId);
        }

        foreach (var (termId, wanted) in desired)
        {
            context.GenreFormProfileEntries.Add(
                new GenreFormProfileEntryEntity
                {
                    TermId = termId,
                    UsageStatus = wanted.Usage,
                    DisplayOrder = wanted.Order,
                    ProfileVersion = GenreFormProfile.Version,
                    UpdatedAt = now
                });

            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
