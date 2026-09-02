using System.Security.Cryptography;
using System.Text.Json;

namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public sealed class ConsumeDocumentManagerResultHandler(
    IDocumentManagerResultSource resultSource,
    IDocumentManagerResultInbox resultInbox,
    IDocumentManagerEditorialDraftPreparer editorialDraftPreparer,
    TimeProvider timeProvider)
{
    public async Task<DocumentManagerConsumeResult> HandleAsync(
        CancellationToken cancellationToken)
    {
        var claim =
            await resultSource.ClaimNextAsync(cancellationToken);

        if (claim is null)
        {
            return new DocumentManagerConsumeResult(
                DocumentManagerConsumeStatus.NoResultAvailable,
                null,
                null,
                null);
        }

        ValidateClaim(claim);

        var payload =
            await resultSource.ReadContentAsync(
                claim,
                cancellationToken);

        VerifyPayload(
            "result content",
            payload,
            claim.ByteLength,
            claim.Sha256);
        VerifyAdvertisedSchemaVersion(
            payload,
            claim.SchemaVersion);

        var descriptors =
            await resultSource.ListVisualAssetsAsync(
                claim,
                cancellationToken);

        ValidateVisualDescriptors(descriptors);

        var visualAssets =
            new List<ReceivedDocumentManagerVisualAsset>(
                descriptors.Count);

        foreach (var descriptor in descriptors)
        {
            var visualPayload =
                await resultSource.ReadVisualAssetAsync(
                    claim,
                    descriptor,
                    cancellationToken);

            VerifyPayload(
                $"visual asset '{descriptor.AssetId}'",
                visualPayload,
                descriptor.ByteLength,
                descriptor.Sha256);

            visualAssets.Add(
                new ReceivedDocumentManagerVisualAsset(
                    descriptor,
                    visualPayload));
        }

        var writeStatus =
            await resultInbox.StoreAsync(
                new ReceivedDocumentManagerResult(
                    claim,
                    payload,
                    visualAssets,
                    timeProvider.GetUtcNow()),
                cancellationToken);

        var draftPreparation =
            await editorialDraftPreparer.PrepareAsync(
                claim.SubmissionId,
                cancellationToken);

        await resultSource.AcknowledgeAsync(
            claim,
            cancellationToken);

        return new DocumentManagerConsumeResult(
            writeStatus == DocumentManagerInboxWriteStatus.Stored
                ? DocumentManagerConsumeStatus.StoredAndAcknowledged
                : DocumentManagerConsumeStatus.AlreadyStoredAndAcknowledged,
            claim.ResultReference,
            claim.SubmissionId,
            draftPreparation);
    }

    private static void ValidateClaim(
        DocumentManagerResultClaim claim)
    {
        RequireText(claim.ResultReference, "Result reference");
        RequireText(claim.Scope.Kind, "Result scope kind");
        RequireText(claim.SchemaVersion, "Schema version");
        RequireText(claim.MediaType, "Media type");
        RequireSha256(claim.Sha256, "Result SHA-256");

        if (claim.SubmissionId == Guid.Empty ||
            claim.ProcessingUnitId == Guid.Empty ||
            claim.ClaimToken == Guid.Empty)
        {
            throw new DocumentManagerResultIntegrityException(
                "The Manager claim contains an empty required identifier.");
        }

        if (claim.ByteLength <= 0)
        {
            throw new DocumentManagerResultIntegrityException(
                "The Manager claim contains an invalid result byte length.");
        }

        if (claim.ClaimExpiresAtUtc <= claim.AvailableAtUtc)
        {
            throw new DocumentManagerResultIntegrityException(
                "The Manager claim expiry is not later than its availability time.");
        }

        ValidateSubmissionManifest(claim);
    }

    private static void ValidateSubmissionManifest(
        DocumentManagerResultClaim claim)
    {
        var manifest = claim.SubmissionManifest ??
            throw new DocumentManagerResultIntegrityException(
                "The Manager claim has no submission manifest.");

        RequireText(manifest.OriginalFileName, "Original filename");
        RequireSha256(manifest.SourceSha256, "Source SHA-256");

        if (manifest.SubmissionId != claim.SubmissionId ||
            manifest.Revision <= 0 ||
            manifest.ExpectedUnits is null ||
            manifest.ExpectedUnits.Count == 0)
        {
            throw new DocumentManagerResultIntegrityException(
                "The Manager claim contains an invalid submission manifest.");
        }

        var identifiers = new HashSet<Guid>();

        for (var index = 0; index < manifest.ExpectedUnits.Count; index++)
        {
            var unit = manifest.ExpectedUnits[index];

            if (unit.ProcessingUnitId == Guid.Empty ||
                unit.Ordinal != index + 1 ||
                !identifiers.Add(unit.ProcessingUnitId))
            {
                throw new DocumentManagerResultIntegrityException(
                    "The submission manifest contains invalid expected processing units.");
            }

            ValidateScope(unit.Scope);
        }

        if (!identifiers.Contains(claim.ProcessingUnitId))
        {
            throw new DocumentManagerResultIntegrityException(
                "The claimed result is not part of its submission manifest.");
        }
    }

    private static void ValidateScope(
        DocumentManagerResultScope scope)
    {
        if (scope is null)
        {
            throw new DocumentManagerResultIntegrityException(
                "The submission manifest contains an empty scope.");
        }

        RequireText(scope.Kind, "Processing-unit scope kind");
    }

    private static void ValidateVisualDescriptors(
        IReadOnlyList<DocumentManagerVisualAssetDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var assetIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            RequireText(descriptor.AssetId, "Visual asset identifier");
            RequireText(descriptor.MediaType, "Visual asset media type");
            RequireSha256(
                descriptor.Sha256,
                $"Visual asset '{descriptor.AssetId}' SHA-256");

            if (descriptor.ByteLength <= 0)
            {
                throw new DocumentManagerResultIntegrityException(
                    $"Visual asset '{descriptor.AssetId}' has an invalid byte length.");
            }

            if (!assetIds.Add(descriptor.AssetId))
            {
                throw new DocumentManagerResultIntegrityException(
                    $"The Manager advertised visual asset '{descriptor.AssetId}' more than once.");
            }
        }
    }

    private static void VerifyPayload(
        string description,
        byte[] payload,
        long expectedLength,
        string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.LongLength != expectedLength)
        {
            throw new DocumentManagerResultIntegrityException(
                $"The {description} length does not match the Manager claim.");
        }

        var actualSha256 =
            Convert.ToHexString(SHA256.HashData(payload))
                .ToLowerInvariant();

        if (!string.Equals(
                actualSha256,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentManagerResultIntegrityException(
                $"The {description} SHA-256 does not match the Manager claim.");
        }
    }

    private static void VerifyAdvertisedSchemaVersion(
        byte[] payload,
        string advertisedSchemaVersion)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(
                    "schemaVersion",
                    out var schemaProperty) ||
                schemaProperty.ValueKind != JsonValueKind.String ||
                !string.Equals(
                    schemaProperty.GetString(),
                    advertisedSchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new DocumentManagerResultIntegrityException(
                    "The result JSON schema version does not match the Manager claim.");
            }
        }
        catch (JsonException exception)
        {
            throw new DocumentManagerResultIntegrityException(
                $"The Manager result is not valid JSON: {exception.Message}");
        }
    }

    private static void RequireText(
        string? value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DocumentManagerResultIntegrityException(
                $"{name} cannot be empty.");
        }
    }

    private static void RequireSha256(
        string? value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length != 64 ||
            value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DocumentManagerResultIntegrityException(
                $"{name} must contain exactly 64 hexadecimal characters.");
        }
    }
}
