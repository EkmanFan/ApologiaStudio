using System.Text.Json;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

public sealed class PostgreSqlDocumentManagerEditorialReviewStore(
    KnowledgeDbContext dbContext)
    : IDocumentManagerEditorialReviewStore
{
    private static readonly JsonSerializerOptions SnapshotOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DocumentManagerEditorialDraftSummary>> ListAsync(
        CancellationToken cancellationToken)
    {
        var drafts =
            await dbContext.DocumentManagerEditorialDrafts
            .AsNoTracking()
            .Include(draft => draft.Parts)
            .OrderBy(
                draft => draft.Status == "pending_review"
                    ? 0
                    : draft.Status == "in_review"
                        ? 1
                        : draft.Status == "approved"
                            ? 2
                            : 3)
            .ThenBy(draft => draft.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return drafts
            .Select(
                draft =>
                    new DocumentManagerEditorialDraftSummary(
                        draft.Id,
                        draft.Title,
                        draft.OriginalFileName,
                        PostgreSqlDocumentManagerEditorialDraftStore
                            .FromPersistenceStatus(draft.Status),
                        draft.Parts.Count,
                        draft.Version,
                        draft.CreatedAtUtc,
                        draft.UpdatedAtUtc))
            .ToArray();
    }

    public async Task<DocumentManagerEditorialDraft?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken)
    {
        if (draftId == Guid.Empty)
        {
            throw new ArgumentException(
                "Editorial draft identifier cannot be empty.",
                nameof(draftId));
        }

        var entity =
            await dbContext.DocumentManagerEditorialDrafts
                .AsNoTracking()
                .Include(draft => draft.Parts)
                .SingleOrDefaultAsync(
                    draft => draft.Id == draftId,
                    cancellationToken);

        return entity is null
            ? null
            : PostgreSqlDocumentManagerEditorialDraftStore.ToContract(entity);
    }

    public async Task<DocumentManagerEditorialDraft> ApplyAsync(
        DocumentManagerEditorialDraftMutation mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        var entity =
            await dbContext.DocumentManagerEditorialDrafts
                .Include(draft => draft.Parts)
                .SingleOrDefaultAsync(
                    draft => draft.Id == mutation.DraftId,
                    cancellationToken) ??
            throw new KeyNotFoundException(
                $"Editorial draft '{mutation.DraftId:D}' was not found.");

        if (entity.Version != mutation.ExpectedVersion)
        {
            throw new DocumentManagerEditorialDraftConcurrencyException(
                mutation.DraftId);
        }

        if (entity.Status is "approved" or "rejected")
        {
            throw new DocumentManagerEditorialReviewValidationException(
                "An approved or rejected editorial draft cannot be modified.");
        }

        var fromStatus = entity.Status;
        var toStatus =
            PostgreSqlDocumentManagerEditorialDraftStore.ToPersistenceStatus(
                mutation.TargetStatus);
        var nextVersion = checked(entity.Version + 1);

        entity.Title = mutation.Title;
        entity.TitleOrigin = mutation.TitleOrigin;
        entity.PrimaryContributorName = mutation.PrimaryContributorName;
        entity.PrimaryContributorRole = mutation.PrimaryContributorRole;
        entity.LanguageCode = mutation.LanguageCode;
        entity.EditionStatement = mutation.EditionStatement;
        entity.PublicationYear = mutation.PublicationYear;
        entity.PublicationPlace = mutation.PublicationPlace;
        entity.Description = mutation.Description;
        entity.Status = toStatus;
        entity.Version = nextVersion;
        entity.LastEditedByUserId = mutation.ActorUserId;
        entity.UpdatedAtUtc = mutation.OccurredAtUtc;
        entity.ReviewedByUserId =
            mutation.TargetStatus is
                DocumentManagerEditorialDraftStatus.Approved or
                DocumentManagerEditorialDraftStatus.Rejected
                ? mutation.ActorUserId
                : null;
        entity.ReviewedAtUtc =
            entity.ReviewedByUserId is not null
                ? mutation.OccurredAtUtc
                : null;
        entity.RejectionReason = mutation.RejectionReason;

        dbContext.DocumentManagerEditorialReviewEvents.Add(
            new DocumentManagerEditorialReviewEventEntity
            {
                DraftId = entity.Id,
                Version = nextVersion,
                Action = ToPersistenceAction(mutation.Action),
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ActorUserId = mutation.ActorUserId,
                OccurredAtUtc = mutation.OccurredAtUtc,
                SnapshotJson = JsonSerializer.Serialize(
                    new
                    {
                        entity.Title,
                        entity.TitleOrigin,
                        entity.PrimaryContributorName,
                        entity.PrimaryContributorRole,
                        entity.LanguageCode,
                        entity.EditionStatement,
                        entity.PublicationYear,
                        entity.PublicationPlace,
                        entity.Description,
                        entity.Status,
                        entity.RejectionReason
                    },
                    SnapshotOptions)
            });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DocumentManagerEditorialDraftConcurrencyException(
                mutation.DraftId);
        }

        return PostgreSqlDocumentManagerEditorialDraftStore.ToContract(entity);
    }

    private static string ToPersistenceAction(
        DocumentManagerEditorialReviewAction action) =>
        action switch
        {
            DocumentManagerEditorialReviewAction.Save => "save",
            DocumentManagerEditorialReviewAction.Approve => "approve",
            DocumentManagerEditorialReviewAction.Reject => "reject",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
}
