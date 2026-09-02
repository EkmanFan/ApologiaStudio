using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

public sealed class PostgreSqlDocumentManagerEditorialAdministrationStore(
    KnowledgeDbContext dbContext)
    : IDocumentManagerEditorialAdministrationStore
{
    public async Task<PurgedDocumentManagerSubmission> PurgeSubmissionAsync(
        PurgeDocumentManagerSubmissionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var selectedDraft = await dbContext.DocumentManagerEditorialDrafts
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM document_manager_editorial_drafts
                WHERE id = {command.DraftId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken) ??
            throw new KeyNotFoundException(
                $"Editorial draft '{command.DraftId:D}' was not found.");

        if (selectedDraft.Version != command.ExpectedVersion)
        {
            throw new DocumentManagerEditorialDraftConcurrencyException(
                command.DraftId);
        }

        var submissionId = selectedDraft.SubmissionId;
        var draftIds = dbContext.DocumentManagerEditorialDrafts
            .Where(draft => draft.SubmissionId == submissionId)
            .Select(draft => draft.Id);
        var resultReferences = dbContext.DocumentManagerResults
            .Where(result => result.SubmissionId == submissionId)
            .Select(result => result.ResultReference);

        var deletedDraftCount = await dbContext.DocumentManagerEditorialDrafts
            .CountAsync(
                draft => draft.SubmissionId == submissionId,
                cancellationToken);
        var deletedResultCount = await dbContext.DocumentManagerResults
            .CountAsync(
                result => result.SubmissionId == submissionId,
                cancellationToken);
        var deletedVisualAssetCount = await dbContext.DocumentManagerVisualAssets
            .CountAsync(
                visual => resultReferences.Contains(visual.ResultReference),
                cancellationToken);
        var deletedManifestCount = await dbContext.DocumentManagerSubmissionManifests
            .CountAsync(
                manifest => manifest.SubmissionId == submissionId,
                cancellationToken);

        await dbContext.DocumentManagerEditorialReviewEvents
            .Where(reviewEvent => draftIds.Contains(reviewEvent.DraftId))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.DocumentManagerEditorialDraftParts
            .Where(part => draftIds.Contains(part.DraftId))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.DocumentManagerEditorialDrafts
            .Where(draft => draft.SubmissionId == submissionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.DocumentManagerVisualAssets
            .Where(visual => resultReferences.Contains(visual.ResultReference))
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.DocumentManagerResults
            .Where(result => result.SubmissionId == submissionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.DocumentManagerExpectedUnits
            .Where(unit => unit.SubmissionId == submissionId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.DocumentManagerSubmissionManifests
            .Where(manifest => manifest.SubmissionId == submissionId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new PurgedDocumentManagerSubmission(
            submissionId,
            deletedDraftCount,
            deletedResultCount,
            deletedVisualAssetCount,
            deletedManifestCount);
    }
}
