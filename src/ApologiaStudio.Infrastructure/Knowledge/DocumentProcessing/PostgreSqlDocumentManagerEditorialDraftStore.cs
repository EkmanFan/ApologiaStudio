using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

public sealed class PostgreSqlDocumentManagerEditorialDraftStore(
    KnowledgeDbContext dbContext)
    : IDocumentManagerEditorialDraftStore
{
    public async Task<DocumentManagerEditorialDraftWriteResult> StoreAsync(
        DocumentManagerEditorialDraft draft,
        CancellationToken cancellationToken)
    {
        Validate(draft);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await AcquireDraftLockAsync(
            draft.SubmissionId,
            draft.ManifestRevision,
            transaction,
            cancellationToken);

        var existing =
            await dbContext.DocumentManagerEditorialDrafts
                .AsNoTracking()
                .Include(item => item.Parts)
                .SingleOrDefaultAsync(
                    item =>
                        item.SubmissionId == draft.SubmissionId &&
                        item.ManifestRevision == draft.ManifestRevision,
                    cancellationToken);

        if (existing is not null)
        {
            ValidateExisting(existing, draft);
            await transaction.CommitAsync(cancellationToken);

            return new DocumentManagerEditorialDraftWriteResult(
                DocumentManagerEditorialDraftWriteStatus.AlreadyExists,
                ToContract(existing));
        }

        var entity = ToEntity(draft);
        dbContext.DocumentManagerEditorialDrafts.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DocumentManagerEditorialDraftWriteResult(
            DocumentManagerEditorialDraftWriteStatus.Created,
            ToContract(entity));
    }

    private async Task AcquireDraftLockAsync(
        Guid submissionId,
        int manifestRevision,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var connection =
            (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var npgsqlTransaction =
            (NpgsqlTransaction)transaction.GetDbTransaction();

        await using var command =
            new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended($1, 0));",
                connection,
                npgsqlTransaction);
        command.Parameters.AddWithValue(
            $"document-manager-draft:{submissionId:D}:{manifestRevision}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DocumentManagerEditorialDraftEntity ToEntity(
        DocumentManagerEditorialDraft draft) =>
        new()
        {
            Id = draft.Id,
            SubmissionId = draft.SubmissionId,
            ManifestRevision = draft.ManifestRevision,
            SourceSha256 = draft.SourceSha256.ToLowerInvariant(),
            OriginalFileName = draft.OriginalFileName,
            Title = draft.Title,
            TitleOrigin = draft.TitleOrigin,
            PrimaryContributorName = draft.PrimaryContributorName,
            PrimaryContributorRole = draft.PrimaryContributorRole,
            LanguageCode = draft.LanguageCode,
            EditionStatement = draft.EditionStatement,
            PublicationYear = draft.PublicationYear,
            PublicationPlace = draft.PublicationPlace,
            Description = draft.Description,
            Status = ToPersistenceStatus(draft.Status),
            Version = draft.Version,
            LastEditedByUserId = draft.LastEditedByUserId,
            ReviewedByUserId = draft.ReviewedByUserId,
            ReviewedAtUtc = draft.ReviewedAtUtc,
            RejectionReason = draft.RejectionReason,
            CreatedAtUtc = draft.CreatedAtUtc,
            UpdatedAtUtc = draft.UpdatedAtUtc,
            Parts = draft.Parts
                .Select(
                    part =>
                    {
                        var scope = part.Scope;
                        return new DocumentManagerEditorialDraftPartEntity
                        {
                            DraftId = draft.Id,
                            ProcessingUnitId = part.ProcessingUnitId,
                            Ordinal = part.Ordinal,
                            ResultReference = part.ResultReference,
                            ScopeKind = scope.Kind,
                            StartPhysicalPageNumber = scope.StartPhysicalPageNumber,
                            EndPhysicalPageNumber = scope.EndPhysicalPageNumber,
                            ScopeTitle = scope.Title,
                            StartContentUnitIndex = scope.StartContentUnitIndex,
                            StartContentUnitId = scope.StartContentUnitId,
                            EndContentUnitIndex = scope.EndContentUnitIndex,
                            EndContentUnitId = scope.EndContentUnitId
                        };
                    })
                .ToList()
        };

    internal static DocumentManagerEditorialDraft ToContract(
        DocumentManagerEditorialDraftEntity entity,
        IReadOnlyList<DocumentManagerEditorialDraftGenreForm>? genreForms = null) =>
        new(
            entity.Id,
            entity.SubmissionId,
            entity.ManifestRevision,
            entity.SourceSha256,
            entity.OriginalFileName,
            entity.Title,
            entity.TitleOrigin,
            entity.PrimaryContributorName,
            entity.PrimaryContributorRole,
            entity.LanguageCode,
            entity.EditionStatement,
            entity.PublicationYear,
            entity.PublicationPlace,
            entity.Description,
            FromPersistenceStatus(entity.Status),
            entity.Version,
            entity.LastEditedByUserId,
            entity.ReviewedByUserId,
            entity.ReviewedAtUtc,
            entity.RejectionReason,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.Parts
                .OrderBy(part => part.Ordinal)
                .Select(
                    part =>
                        new DocumentManagerEditorialDraftPart(
                            part.ProcessingUnitId,
                            part.Ordinal,
                            part.ResultReference,
                            ToScope(part)))
                .ToArray(),
            genreForms ?? []);

    private static DocumentManagerResultScope ToScope(
        DocumentManagerEditorialDraftPartEntity part) =>
        new(
            part.ScopeKind,
            part.StartPhysicalPageNumber,
            part.EndPhysicalPageNumber,
            part.ScopeTitle,
            part.StartContentUnitIndex,
            part.StartContentUnitId,
            part.EndContentUnitIndex,
            part.EndContentUnitId);

    private static void ValidateExisting(
        DocumentManagerEditorialDraftEntity existing,
        DocumentManagerEditorialDraft received)
    {
        var matches =
            existing.Id == received.Id &&
            string.Equals(
                existing.SourceSha256,
                received.SourceSha256,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                existing.OriginalFileName,
                received.OriginalFileName,
                StringComparison.Ordinal) &&
            existing.Parts.Count == received.Parts.Count;

        if (matches)
        {
            var receivedParts = received.Parts.ToDictionary(
                part => part.ProcessingUnitId);
            matches = existing.Parts.All(
                part =>
                    receivedParts.TryGetValue(
                        part.ProcessingUnitId,
                        out var candidate) &&
                    part.Ordinal == candidate.Ordinal &&
                    string.Equals(
                        part.ResultReference,
                        candidate.ResultReference,
                        StringComparison.Ordinal) &&
                    ScopeMatches(part, candidate.Scope));
        }

        if (!matches)
        {
            throw new DocumentManagerResultIntegrityException(
                $"Editorial draft for submission '{received.SubmissionId:D}' revision {received.ManifestRevision} already exists with different source evidence.");
        }
    }

    private static bool ScopeMatches(
        DocumentManagerEditorialDraftPartEntity existing,
        DocumentManagerResultScope received) =>
        string.Equals(existing.ScopeKind, received.Kind, StringComparison.Ordinal) &&
        existing.StartPhysicalPageNumber == received.StartPhysicalPageNumber &&
        existing.EndPhysicalPageNumber == received.EndPhysicalPageNumber &&
        string.Equals(existing.ScopeTitle, received.Title, StringComparison.Ordinal) &&
        existing.StartContentUnitIndex == received.StartContentUnitIndex &&
        string.Equals(existing.StartContentUnitId, received.StartContentUnitId, StringComparison.Ordinal) &&
        existing.EndContentUnitIndex == received.EndContentUnitIndex &&
        string.Equals(existing.EndContentUnitId, received.EndContentUnitId, StringComparison.Ordinal);

    private static void Validate(DocumentManagerEditorialDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.SourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.OriginalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.TitleOrigin);

        if (draft.Id == Guid.Empty || draft.SubmissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Editorial draft identifiers cannot be empty.",
                nameof(draft));
        }

        if (draft.ManifestRevision <= 0 || draft.Version < 0)
        {
            throw new ArgumentException(
                "Editorial draft revision and version are invalid.",
                nameof(draft));
        }

        if (draft.Parts.Count == 0 ||
            draft.Parts.OrderBy(part => part.Ordinal)
                .Select(part => part.Ordinal)
                .Where((ordinal, index) => ordinal != index + 1)
                .Any())
        {
            throw new ArgumentException(
                "Editorial draft parts must use contiguous one-based ordinals.",
                nameof(draft));
        }
    }

    internal static string ToPersistenceStatus(
        DocumentManagerEditorialDraftStatus status) =>
        status switch
        {
            DocumentManagerEditorialDraftStatus.PendingReview => "pending_review",
            DocumentManagerEditorialDraftStatus.InReview => "in_review",
            DocumentManagerEditorialDraftStatus.Approved => "approved",
            DocumentManagerEditorialDraftStatus.Rejected => "rejected",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    internal static DocumentManagerEditorialDraftStatus FromPersistenceStatus(
        string status) =>
        status switch
        {
            "pending_review" => DocumentManagerEditorialDraftStatus.PendingReview,
            "in_review" => DocumentManagerEditorialDraftStatus.InReview,
            "approved" => DocumentManagerEditorialDraftStatus.Approved,
            "rejected" => DocumentManagerEditorialDraftStatus.Rejected,
            _ => throw new DocumentManagerResultIntegrityException(
                $"Stored editorial draft has unknown status '{status}'.")
        };
}
