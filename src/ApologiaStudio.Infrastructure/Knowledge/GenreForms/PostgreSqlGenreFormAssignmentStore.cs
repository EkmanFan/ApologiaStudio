using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.GenreForms;

/// <summary>
/// Explicit Work to Genre/Form assignment.
///
/// Nothing is ever inferred: a broader term is never persisted because a
/// narrower one was assigned, and no assignment is created automatically.
/// </summary>
public sealed class PostgreSqlGenreFormAssignmentStore(
    KnowledgeDbContext context,
    IGenreFormAuthorityStore authorityStore)
    : IGenreFormAssignmentStore
{
    public async Task<IReadOnlyList<GenreFormTermView>> GetWorkGenreFormsAsync(
        Guid workId,
        CancellationToken cancellationToken)
    {
        var uris = await (
            from assignment in context.WorkGenreForms.AsNoTracking()
            join term in context.GenreFormTerms.AsNoTracking()
                on assignment.TermId equals term.Id
            where assignment.WorkId == workId
            select term.AuthorityUri)
            .ToListAsync(cancellationToken);

        var views = new List<GenreFormTermView>();

        foreach (var uri in uris)
        {
            var view = await authorityStore.GetTermByAuthorityUriAsync(
                uri,
                cancellationToken);

            if (view is not null)
            {
                views.Add(view);
            }
        }

        return views
            .OrderBy(x => x.PreferredLabel, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<GenreFormAssignmentResult> AssignAsync(
        Guid workId,
        string authorityUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityUri);

        var term = await authorityStore.GetTermByAuthorityUriAsync(
            authorityUri,
            cancellationToken);

        if (term is null)
        {
            return new GenreFormAssignmentResult(
                false,
                "The term is absent from the imported authority.");
        }

        if (term.UsageStatus != GenreFormUsageStatus.Selectable)
        {
            // Closed vocabulary: only approved terms reach a Work.
            return new GenreFormAssignmentResult(
                false,
                "The term is not selectable in the active Apologia profile.");
        }

        var workExists = await context.Works
            .AsNoTracking()
            .AnyAsync(x => x.Id == workId, cancellationToken);

        if (!workExists)
        {
            return new GenreFormAssignmentResult(false, "Unknown work.");
        }

        var alreadyAssigned = await context.WorkGenreForms
            .AsNoTracking()
            .AnyAsync(
                x => x.WorkId == workId && x.TermId == term.Id,
                cancellationToken);

        if (alreadyAssigned)
        {
            return new GenreFormAssignmentResult(
                false,
                "The work already carries this genre/form.");
        }

        var conflict = await FindHierarchyConflictAsync(
            workId,
            term,
            cancellationToken);

        if (conflict is not null)
        {
            return new GenreFormAssignmentResult(false, conflict);
        }

        context.WorkGenreForms.Add(
            new KnowledgeWorkGenreFormEntity
            {
                WorkId = workId,
                TermId = term.Id
            });

        await context.SaveChangesAsync(cancellationToken);

        return new GenreFormAssignmentResult(true, "Assigned.");
    }

    public async Task<bool> RemoveAsync(
        Guid workId,
        string authorityUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityUri);

        var removed = await context.WorkGenreForms
            .Where(x => x.WorkId == workId)
            .Where(x => context.GenreFormTerms
                .Any(t => t.Id == x.TermId && t.AuthorityUri == authorityUri))
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    /// <summary>
    /// Keeps only the most specific applicable term on one ancestor path. Two
    /// unrelated genres may coexist; a term and its ancestor may not.
    /// </summary>
    private async Task<string?> FindHierarchyConflictAsync(
        Guid workId,
        GenreFormTermView candidate,
        CancellationToken cancellationToken)
    {
        var assignedIds = await context.WorkGenreForms
            .AsNoTracking()
            .Where(x => x.WorkId == workId)
            .Select(x => x.TermId)
            .ToListAsync(cancellationToken);

        if (assignedIds.Count == 0)
        {
            return null;
        }

        var ancestorsOfCandidate = await AncestorsAsync(candidate.Id, cancellationToken);

        foreach (var assignedId in assignedIds)
        {
            if (ancestorsOfCandidate.Contains(assignedId))
            {
                return "A broader term is already assigned; remove it before " +
                       "assigning this more specific term.";
            }

            var ancestorsOfAssigned = await AncestorsAsync(assignedId, cancellationToken);

            if (ancestorsOfAssigned.Contains(candidate.Id))
            {
                return "A more specific term is already assigned; the broader " +
                       "term must not be persisted as well.";
            }
        }

        return null;
    }

    private async Task<HashSet<Guid>> AncestorsAsync(
        Guid termId,
        CancellationToken cancellationToken)
    {
        var ancestors = new HashSet<Guid>();
        var frontier = new List<Guid> { termId };

        // The thesaurus is polyhierarchical but shallow; a bounded walk is
        // enough and avoids a recursive query for a handful of rows.
        for (var depth = 0; depth < 16 && frontier.Count > 0; depth++)
        {
            var parents = await context.GenreFormBroaderRelations
                .AsNoTracking()
                .Where(x => frontier.Contains(x.NarrowerTermId))
                .Select(x => x.BroaderTermId)
                .Distinct()
                .ToListAsync(cancellationToken);

            frontier = parents.Where(ancestors.Add).ToList();
        }

        return ancestors;
    }
}
