using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.GenreForms;

/// <summary>
/// Explicit Work to product Genre/Form assignment.
/// </summary>
/// <remarks>
/// A Work carries Apologia product concepts. The external authority catalogue
/// is not consulted here at all: an assignment that depended on LCGFT being
/// imported would make the product's own vocabulary hostage to a maintenance
/// operation on someone else's thesaurus.
///
/// The V1 taxonomy is flat, so the earlier ancestor-conflict rule is gone
/// rather than reimplemented: with no product hierarchy there is no ancestor
/// path on which a term could be redundant, and inventing one would be a
/// design decision this slice has no mandate to make.
/// </remarks>
public sealed class PostgreSqlWorkGenreFormAssignmentStore(
    KnowledgeDbContext context)
    : IWorkGenreFormAssignmentStore
{
    #region Methods

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApologiaGenreFormTerm>> GetWorkGenreFormsAsync(
        Guid workId,
        CancellationToken cancellationToken)
    {
        var codes = await (
            from assignment in context.WorkGenreForms.AsNoTracking()
            join term in context.ApologiaGenreFormTerms.AsNoTracking()
                on assignment.ProductTermId equals term.Id
            where assignment.WorkId == workId
            select term.Code)
            .ToListAsync(cancellationToken);

        return ApologiaGenreFormTaxonomy.Terms
            .Where(x => codes.Contains(x.Code))
            .OrderBy(x => x.DisplayOrder)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<GenreFormAssignmentResult> AssignAsync(
        Guid workId,
        string productTermCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productTermCode);

        var term = await context.ApologiaGenreFormTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Code == productTermCode && x.Status == "active",
                cancellationToken);

        if (term is null)
        {
            // Closed vocabulary: only an active canonical term reaches a Work.
            return new GenreFormAssignmentResult(
                false,
                "The term is not an active Apologia genre/form.");
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
                x => x.WorkId == workId && x.ProductTermId == term.Id,
                cancellationToken);

        if (alreadyAssigned)
        {
            return new GenreFormAssignmentResult(
                false,
                "The work already carries this genre/form.");
        }

        context.WorkGenreForms.Add(
            new KnowledgeWorkGenreFormEntity
            {
                WorkId = workId,
                ProductTermId = term.Id
            });

        await context.SaveChangesAsync(cancellationToken);

        return new GenreFormAssignmentResult(true, "Assigned.");
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(
        Guid workId,
        string productTermCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productTermCode);

        var removed = await context.WorkGenreForms
            .Where(x => x.WorkId == workId)
            .Where(x => context.ApologiaGenreFormTerms
                .Any(t => t.Id == x.ProductTermId && t.Code == productTermCode))
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    #endregion
}
