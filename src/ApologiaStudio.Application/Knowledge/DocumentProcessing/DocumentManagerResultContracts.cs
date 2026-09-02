namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public sealed record DocumentManagerResultScope(
    string Kind,
    int? StartPhysicalPageNumber,
    int? EndPhysicalPageNumber,
    string? Title,
    int? StartContentUnitIndex,
    string? StartContentUnitId,
    int? EndContentUnitIndex,
    string? EndContentUnitId);

public sealed record DocumentManagerResultClaim(
    string ResultReference,
    Guid SubmissionId,
    Guid ProcessingUnitId,
    DocumentManagerResultScope Scope,
    string SchemaVersion,
    string MediaType,
    long ByteLength,
    string Sha256,
    DateTimeOffset AvailableAtUtc,
    Guid ClaimToken,
    DateTimeOffset ClaimExpiresAtUtc,
    DocumentManagerSubmissionManifest SubmissionManifest);

public sealed record DocumentManagerSubmissionManifest(
    Guid SubmissionId,
    int Revision,
    string SourceSha256,
    string OriginalFileName,
    DateTimeOffset FinalizedAtUtc,
    IReadOnlyList<DocumentManagerExpectedProcessingUnit> ExpectedUnits);

public sealed record DocumentManagerExpectedProcessingUnit(
    Guid ProcessingUnitId,
    int Ordinal,
    DocumentManagerResultScope Scope);

public sealed record DocumentManagerVisualAssetDescriptor(
    string AssetId,
    string MediaType,
    long ByteLength,
    string Sha256);

public sealed record ReceivedDocumentManagerVisualAsset(
    DocumentManagerVisualAssetDescriptor Descriptor,
    byte[] Payload);

public sealed record ReceivedDocumentManagerResult(
    DocumentManagerResultClaim Claim,
    byte[] Payload,
    IReadOnlyList<ReceivedDocumentManagerVisualAsset> VisualAssets,
    DateTimeOffset ReceivedAtUtc);

public enum DocumentManagerInboxWriteStatus
{
    Stored = 0,
    AlreadyStored = 1
}

public enum DocumentManagerConsumeStatus
{
    NoResultAvailable = 0,
    StoredAndAcknowledged = 1,
    AlreadyStoredAndAcknowledged = 2
}

public sealed record DocumentManagerConsumeResult(
    DocumentManagerConsumeStatus Status,
    string? ResultReference,
    Guid? SubmissionId,
    DocumentManagerEditorialDraftPreparationResult? DraftPreparation);
