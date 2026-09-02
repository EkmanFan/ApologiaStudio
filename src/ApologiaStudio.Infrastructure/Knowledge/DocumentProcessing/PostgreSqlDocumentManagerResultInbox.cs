using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

public sealed class PostgreSqlDocumentManagerResultInbox(
    KnowledgeDbContext dbContext)
    : IDocumentManagerResultInbox
{
    public async Task<DocumentManagerInboxWriteStatus> StoreAsync(
        ReceivedDocumentManagerResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await AcquireResultLockAsync(
            result.Claim.ResultReference,
            transaction,
            cancellationToken);

        await AcquireSubmissionLockAsync(
            result.Claim.SubmissionId,
            transaction,
            cancellationToken);

        await StoreManifestAsync(
            result.Claim.SubmissionManifest,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var existing =
            await dbContext.DocumentManagerResults
                .AsNoTracking()
                .Include(item => item.VisualAssets)
                .SingleOrDefaultAsync(
                    item =>
                        item.ResultReference == result.Claim.ResultReference,
                    cancellationToken);

        if (existing is not null)
        {
            ValidateExisting(existing, result);
            await transaction.CommitAsync(cancellationToken);
            return DocumentManagerInboxWriteStatus.AlreadyStored;
        }

        var scope = result.Claim.Scope;
        var entity =
            new DocumentManagerResultInboxEntity
            {
                ResultReference = result.Claim.ResultReference,
                SubmissionId = result.Claim.SubmissionId,
                ProcessingUnitId = result.Claim.ProcessingUnitId,
                ScopeKind = scope.Kind,
                StartPhysicalPageNumber = scope.StartPhysicalPageNumber,
                EndPhysicalPageNumber = scope.EndPhysicalPageNumber,
                ScopeTitle = scope.Title,
                StartContentUnitIndex = scope.StartContentUnitIndex,
                StartContentUnitId = scope.StartContentUnitId,
                EndContentUnitIndex = scope.EndContentUnitIndex,
                EndContentUnitId = scope.EndContentUnitId,
                SchemaVersion = result.Claim.SchemaVersion,
                MediaType = result.Claim.MediaType,
                ByteLength = result.Claim.ByteLength,
                Sha256 = result.Claim.Sha256.ToLowerInvariant(),
                AvailableAtUtc = result.Claim.AvailableAtUtc,
                ReceivedAtUtc = result.ReceivedAtUtc,
                Payload = result.Payload.ToArray(),
                VisualAssets = result.VisualAssets
                    .Select(asset =>
                        new DocumentManagerVisualAssetInboxEntity
                        {
                            ResultReference = result.Claim.ResultReference,
                            AssetId = asset.Descriptor.AssetId,
                            MediaType = asset.Descriptor.MediaType,
                            ByteLength = asset.Descriptor.ByteLength,
                            Sha256 = asset.Descriptor.Sha256.ToLowerInvariant(),
                            Payload = asset.Payload.ToArray()
                        })
                    .ToList()
            };

        dbContext.DocumentManagerResults.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return DocumentManagerInboxWriteStatus.Stored;
    }

    private async Task AcquireResultLockAsync(
        string resultReference,
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
        command.Parameters.AddWithValue(resultReference);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AcquireSubmissionLockAsync(
        Guid submissionId,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var connection =
            (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var npgsqlTransaction =
            (NpgsqlTransaction)transaction.GetDbTransaction();

        await using var command =
            new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended($1::text, 0));",
                connection,
                npgsqlTransaction);
        command.Parameters.AddWithValue(submissionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task StoreManifestAsync(
        DocumentManagerSubmissionManifest manifest,
        CancellationToken cancellationToken)
    {
        var existing =
            await dbContext.DocumentManagerSubmissionManifests
                .AsNoTracking()
                .Include(item => item.ExpectedUnits)
                .SingleOrDefaultAsync(
                    item =>
                        item.SubmissionId == manifest.SubmissionId &&
                        item.Revision == manifest.Revision,
                    cancellationToken);

        if (existing is not null)
        {
            ValidateExistingManifest(existing, manifest);
            return;
        }

        dbContext.DocumentManagerSubmissionManifests.Add(
            new DocumentManagerSubmissionManifestInboxEntity
            {
                SubmissionId = manifest.SubmissionId,
                Revision = manifest.Revision,
                SourceSha256 = manifest.SourceSha256.ToLowerInvariant(),
                OriginalFileName = manifest.OriginalFileName,
                FinalizedAtUtc = manifest.FinalizedAtUtc,
                ExpectedUnits = manifest.ExpectedUnits
                    .Select(
                        unit =>
                        {
                            var scope = unit.Scope;
                            return new DocumentManagerExpectedUnitInboxEntity
                            {
                                SubmissionId = manifest.SubmissionId,
                                ManifestRevision = manifest.Revision,
                                ProcessingUnitId = unit.ProcessingUnitId,
                                Ordinal = unit.Ordinal,
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
            });
    }

    private static void ValidateExistingManifest(
        DocumentManagerSubmissionManifestInboxEntity existing,
        DocumentManagerSubmissionManifest received)
    {
        var matches =
            string.Equals(existing.SourceSha256, received.SourceSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.OriginalFileName, received.OriginalFileName, StringComparison.Ordinal) &&
            existing.FinalizedAtUtc == received.FinalizedAtUtc &&
            existing.ExpectedUnits.Count == received.ExpectedUnits.Count;

        if (matches)
        {
            var receivedById = received.ExpectedUnits.ToDictionary(unit => unit.ProcessingUnitId);
            matches = existing.ExpectedUnits.All(
                unit =>
                    receivedById.TryGetValue(unit.ProcessingUnitId, out var candidate) &&
                    unit.Ordinal == candidate.Ordinal &&
                    ScopeMatches(unit, candidate.Scope));
        }

        if (!matches)
        {
            throw new DocumentManagerResultIntegrityException(
                $"Submission manifest '{received.SubmissionId:D}' revision {received.Revision} already exists with different custody data.");
        }
    }

    private static bool ScopeMatches(
        DocumentManagerExpectedUnitInboxEntity existing,
        DocumentManagerResultScope received) =>
        string.Equals(existing.ScopeKind, received.Kind, StringComparison.Ordinal) &&
        existing.StartPhysicalPageNumber == received.StartPhysicalPageNumber &&
        existing.EndPhysicalPageNumber == received.EndPhysicalPageNumber &&
        string.Equals(existing.ScopeTitle, received.Title, StringComparison.Ordinal) &&
        existing.StartContentUnitIndex == received.StartContentUnitIndex &&
        string.Equals(existing.StartContentUnitId, received.StartContentUnitId, StringComparison.Ordinal) &&
        existing.EndContentUnitIndex == received.EndContentUnitIndex &&
        string.Equals(existing.EndContentUnitId, received.EndContentUnitId, StringComparison.Ordinal);

    private static void ValidateExisting(
        DocumentManagerResultInboxEntity existing,
        ReceivedDocumentManagerResult received)
    {
        var claim = received.Claim;
        var scope = claim.Scope;

        var matches =
            existing.SubmissionId == claim.SubmissionId &&
            existing.ProcessingUnitId == claim.ProcessingUnitId &&
            string.Equals(existing.ScopeKind, scope.Kind, StringComparison.Ordinal) &&
            existing.StartPhysicalPageNumber == scope.StartPhysicalPageNumber &&
            existing.EndPhysicalPageNumber == scope.EndPhysicalPageNumber &&
            string.Equals(existing.ScopeTitle, scope.Title, StringComparison.Ordinal) &&
            existing.StartContentUnitIndex == scope.StartContentUnitIndex &&
            string.Equals(existing.StartContentUnitId, scope.StartContentUnitId, StringComparison.Ordinal) &&
            existing.EndContentUnitIndex == scope.EndContentUnitIndex &&
            string.Equals(existing.EndContentUnitId, scope.EndContentUnitId, StringComparison.Ordinal) &&
            string.Equals(existing.SchemaVersion, claim.SchemaVersion, StringComparison.Ordinal) &&
            string.Equals(existing.MediaType, claim.MediaType, StringComparison.OrdinalIgnoreCase) &&
            existing.ByteLength == claim.ByteLength &&
            string.Equals(existing.Sha256, claim.Sha256, StringComparison.OrdinalIgnoreCase) &&
            existing.AvailableAtUtc == claim.AvailableAtUtc &&
            existing.Payload.AsSpan().SequenceEqual(received.Payload);

        if (!matches)
        {
            throw new DocumentManagerResultIntegrityException(
                $"Result reference '{claim.ResultReference}' already exists with different custody data.");
        }

        var receivedAssets = received.VisualAssets.ToDictionary(
            asset => asset.Descriptor.AssetId,
            StringComparer.Ordinal);

        if (existing.VisualAssets.Count != receivedAssets.Count)
        {
            throw new DocumentManagerResultIntegrityException(
                $"Result reference '{claim.ResultReference}' already exists with a different visual manifest.");
        }

        foreach (var existingAsset in existing.VisualAssets)
        {
            if (!receivedAssets.TryGetValue(
                    existingAsset.AssetId,
                    out var receivedAsset) ||
                !string.Equals(
                    existingAsset.MediaType,
                    receivedAsset.Descriptor.MediaType,
                    StringComparison.OrdinalIgnoreCase) ||
                existingAsset.ByteLength != receivedAsset.Descriptor.ByteLength ||
                !string.Equals(
                    existingAsset.Sha256,
                    receivedAsset.Descriptor.Sha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !existingAsset.Payload.AsSpan()
                    .SequenceEqual(receivedAsset.Payload))
            {
                throw new DocumentManagerResultIntegrityException(
                    $"Result reference '{claim.ResultReference}' already exists with different visual custody data.");
            }
        }
    }
}
