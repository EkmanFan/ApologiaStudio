namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public enum DocumentManagerEditorialDraftStatus
{
    PendingReview = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3
}

public sealed record DocumentManagerEditorialDraft(
    Guid Id,
    Guid SubmissionId,
    int ManifestRevision,
    string SourceSha256,
    string OriginalFileName,
    string Title,
    string TitleOrigin,
    string? PrimaryContributorName,
    string? PrimaryContributorRole,
    string? LanguageCode,
    string? EditionStatement,
    int? PublicationYear,
    string? PublicationPlace,
    string? Description,
    DocumentManagerEditorialDraftStatus Status,
    int Version,
    Guid? LastEditedByUserId,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? RejectionReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<DocumentManagerEditorialDraftPart> Parts);

public sealed record DocumentManagerEditorialDraftPart(
    Guid ProcessingUnitId,
    int Ordinal,
    string ResultReference,
    DocumentManagerResultScope Scope);

public enum DocumentManagerEditorialDraftWriteStatus
{
    Created = 0,
    AlreadyExists = 1
}

public sealed record DocumentManagerEditorialDraftWriteResult(
    DocumentManagerEditorialDraftWriteStatus Status,
    DocumentManagerEditorialDraft Draft);

public enum DocumentManagerEditorialDraftPreparationStatus
{
    AwaitingParts = 0,
    Blocked = 1,
    Created = 2,
    AlreadyExists = 3
}

public sealed record DocumentManagerEditorialDraftPreparationResult(
    DocumentManagerEditorialDraftPreparationStatus Status,
    DocumentManagerSubmissionAssembly Assembly,
    DocumentManagerEditorialDraft? Draft);
