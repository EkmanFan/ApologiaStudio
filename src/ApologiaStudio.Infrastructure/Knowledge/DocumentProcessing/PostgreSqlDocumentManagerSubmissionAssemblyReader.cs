using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

public sealed class PostgreSqlDocumentManagerSubmissionAssemblyReader(
    KnowledgeDbContext dbContext)
    : IDocumentManagerSubmissionAssemblyReader
{
    public async Task<DocumentManagerSubmissionAssembly?> GetAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Submission identifier cannot be empty.",
                nameof(submissionId));
        }

        var entity =
            await dbContext.DocumentManagerSubmissionManifests
                .AsNoTracking()
                .Include(manifest => manifest.ExpectedUnits)
                .Where(manifest => manifest.SubmissionId == submissionId)
                .OrderByDescending(manifest => manifest.Revision)
                .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var manifest =
            new DocumentManagerSubmissionManifest(
                entity.SubmissionId,
                entity.Revision,
                entity.SourceSha256,
                entity.OriginalFileName,
                entity.FinalizedAtUtc,
                entity.ExpectedUnits
                    .OrderBy(unit => unit.Ordinal)
                    .Select(
                        unit =>
                            new DocumentManagerExpectedProcessingUnit(
                                unit.ProcessingUnitId,
                                unit.Ordinal,
                                ToScope(unit)))
                    .ToArray());

        var results =
            await dbContext.DocumentManagerResults
                .AsNoTracking()
                .Where(result => result.SubmissionId == submissionId)
                .Select(
                    result =>
                        new DocumentManagerStoredResultSummary(
                            result.ResultReference,
                            result.ProcessingUnitId,
                            new DocumentManagerResultScope(
                                result.ScopeKind,
                                result.StartPhysicalPageNumber,
                                result.EndPhysicalPageNumber,
                                result.ScopeTitle,
                                result.StartContentUnitIndex,
                                result.StartContentUnitId,
                                result.EndContentUnitIndex,
                                result.EndContentUnitId)))
                .ToArrayAsync(cancellationToken);

        return DocumentManagerSubmissionAssembler.Assemble(
            manifest,
            results);
    }

    private static DocumentManagerResultScope ToScope(
        DocumentManagerExpectedUnitInboxEntity unit) =>
        new(
            unit.ScopeKind,
            unit.StartPhysicalPageNumber,
            unit.EndPhysicalPageNumber,
            unit.ScopeTitle,
            unit.StartContentUnitIndex,
            unit.StartContentUnitId,
            unit.EndContentUnitIndex,
            unit.EndContentUnitId);
}
