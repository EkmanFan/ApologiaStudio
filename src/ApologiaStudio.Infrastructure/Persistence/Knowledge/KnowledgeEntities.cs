using Pgvector;

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge;

internal sealed class KnowledgeResourceEntity
{
    public Guid Id { get; set; }
    public string EditorialReviewStatus { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class KnowledgeWorkEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OriginalLanguage { get; set; }
    public string? Description { get; set; }
}

internal sealed class KnowledgeExpressionEntity
{
    public Guid Id { get; set; }
    public Guid WorkId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Description { get; set; }
}

internal sealed class KnowledgeExpressionRelationEntity
{
    public long Id { get; set; }
    public Guid FromExpressionId { get; set; }
    public Guid ToExpressionId { get; set; }
    public string RelationType { get; set; } = string.Empty;
}

internal sealed class KnowledgeManifestationEntity
{
    public Guid Id { get; set; }
    public Guid ExpressionId { get; set; }
    public string? EditionStatement { get; set; }
    public int? PublicationYear { get; set; }
    public string? PublicationPlace { get; set; }
    public string? CitationLabel { get; set; }
}

internal sealed class KnowledgeManifestationIdentifierEntity
{
    public long Id { get; set; }
    public Guid ManifestationId { get; set; }
    public string Scheme { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Uri { get; set; }
}

internal sealed class KnowledgeContributorEntity
{
    public Guid Id { get; set; }
    public string ContributorType { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public string? SortName { get; set; }
    public string? Description { get; set; }
}

internal sealed class KnowledgeContributorIdentifierEntity
{
    public long Id { get; set; }
    public Guid ContributorId { get; set; }
    public string Scheme { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Uri { get; set; }
}

internal sealed class KnowledgeContributionEntity
{
    public long Id { get; set; }
    public Guid ContributorId { get; set; }
    public Guid? WorkId { get; set; }
    public Guid? ExpressionId { get; set; }
    public Guid? ManifestationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string AttributionStatus { get; set; } = string.Empty;
    public int Ordinal { get; set; }
}

internal sealed class KnowledgeArtifactEntity
{
    public Guid Id { get; set; }
    public Guid ManifestationId { get; set; }
    public Guid? DerivedFromArtifactId { get; set; }
    public string ArtifactType { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public string? OriginUri { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }
    public string LifecycleStatus { get; set; } = "active";
}

internal sealed class KnowledgeProcessingActivityEntity
{
    public long Id { get; set; }
    public Guid? InputArtifactId { get; set; }
    public Guid OutputArtifactId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ToolVersion { get; set; } = string.Empty;
    public string? ConfigurationJson { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ExecutedBy { get; set; }
    public string Status { get; set; } = "pending";
}

internal sealed class KnowledgeDocumentSegmentEntity
{
    public Guid Id { get; set; }
    public Guid ArtifactId { get; set; }
    public Guid? ParentSegmentId { get; set; }
    public string SegmentType { get; set; } = string.Empty;
    public string SegmentKind { get; set; } = "unknown";
    public int Ordinal { get; set; }
    public string? Title { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Locator { get; set; }
}

internal sealed class KnowledgeRetrievalChunkEntity
{
    public Guid Id { get; set; }
    public Guid ArtifactId { get; set; }
    public int Ordinal { get; set; }
    public string Text { get; set; } = string.Empty;
    public string ChunkingStrategy { get; set; } = string.Empty;
    public string ChunkingVersion { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class KnowledgeRetrievalChunkSegmentEntity
{
    public Guid ChunkId { get; set; }
    public Guid SegmentId { get; set; }
    public int Sequence { get; set; }
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
}

internal sealed class KnowledgeChunkEmbeddingEntity
{
    public Guid Id { get; set; }
    public Guid ChunkId { get; set; }
    public string EmbeddingProfile { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ModelDigest { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public Vector Embedding { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class DocumentManagerResultInboxEntity
{
    public string ResultReference { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
    public Guid ProcessingUnitId { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public int? StartPhysicalPageNumber { get; set; }
    public int? EndPhysicalPageNumber { get; set; }
    public string? ScopeTitle { get; set; }
    public int? StartContentUnitIndex { get; set; }
    public string? StartContentUnitId { get; set; }
    public int? EndContentUnitIndex { get; set; }
    public string? EndContentUnitId { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTimeOffset AvailableAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public byte[] Payload { get; set; } = [];
    public ICollection<DocumentManagerVisualAssetInboxEntity> VisualAssets { get; set; } =
        new List<DocumentManagerVisualAssetInboxEntity>();
}

internal sealed class DocumentManagerSubmissionManifestInboxEntity
{
    public Guid SubmissionId { get; set; }
    public int Revision { get; set; }
    public string SourceSha256 { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTimeOffset FinalizedAtUtc { get; set; }
    public ICollection<DocumentManagerExpectedUnitInboxEntity> ExpectedUnits { get; set; } =
        new List<DocumentManagerExpectedUnitInboxEntity>();
}

internal sealed class DocumentManagerExpectedUnitInboxEntity
{
    public Guid SubmissionId { get; set; }
    public int ManifestRevision { get; set; }
    public Guid ProcessingUnitId { get; set; }
    public int Ordinal { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public int? StartPhysicalPageNumber { get; set; }
    public int? EndPhysicalPageNumber { get; set; }
    public string? ScopeTitle { get; set; }
    public int? StartContentUnitIndex { get; set; }
    public string? StartContentUnitId { get; set; }
    public int? EndContentUnitIndex { get; set; }
    public string? EndContentUnitId { get; set; }
    public DocumentManagerSubmissionManifestInboxEntity Manifest { get; set; } = null!;
}

internal sealed class DocumentManagerEditorialDraftEntity
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int ManifestRevision { get; set; }
    public string SourceSha256 { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleOrigin { get; set; } = "original_filename";
    public string? PrimaryContributorName { get; set; }
    public string? PrimaryContributorRole { get; set; }
    public string? LanguageCode { get; set; }
    public string? EditionStatement { get; set; }
    public int? PublicationYear { get; set; }
    public string? PublicationPlace { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "pending_review";
    public int Version { get; set; }
    public Guid? LastEditedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DocumentManagerSubmissionManifestInboxEntity Manifest { get; set; } = null!;
    public ICollection<DocumentManagerEditorialDraftPartEntity> Parts { get; set; } =
        new List<DocumentManagerEditorialDraftPartEntity>();
    public ICollection<DocumentManagerEditorialReviewEventEntity> ReviewEvents { get; set; } =
        new List<DocumentManagerEditorialReviewEventEntity>();
}

internal sealed class DocumentManagerEditorialDraftPartEntity
{
    public Guid DraftId { get; set; }
    public Guid ProcessingUnitId { get; set; }
    public int Ordinal { get; set; }
    public string ResultReference { get; set; } = string.Empty;
    public string ScopeKind { get; set; } = string.Empty;
    public int? StartPhysicalPageNumber { get; set; }
    public int? EndPhysicalPageNumber { get; set; }
    public string? ScopeTitle { get; set; }
    public int? StartContentUnitIndex { get; set; }
    public string? StartContentUnitId { get; set; }
    public int? EndContentUnitIndex { get; set; }
    public string? EndContentUnitId { get; set; }
    public DocumentManagerEditorialDraftEntity Draft { get; set; } = null!;
    public DocumentManagerResultInboxEntity Result { get; set; } = null!;
}

internal sealed class DocumentManagerEditorialReviewEventEntity
{
    public long Id { get; set; }
    public Guid DraftId { get; set; }
    public int Version { get; set; }
    public string Action { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public DocumentManagerEditorialDraftEntity Draft { get; set; } = null!;
}

internal sealed class DocumentManagerVisualAssetInboxEntity
{
    public string ResultReference { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public byte[] Payload { get; set; } = [];
    public DocumentManagerResultInboxEntity Result { get; set; } = null!;
}

internal sealed class KnowledgeMetadataAssertionEntity
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string Property { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string AssertionOrigin { get; set; } = string.Empty;
    public string AssertedBy { get; set; } = string.Empty;
    public DateTimeOffset AssertedAt { get; set; }
    public string ReviewStatus { get; set; } = "proposed";
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public double? Confidence { get; set; }
    public string? Justification { get; set; }
    public Guid? SupportingSegmentId { get; set; }
    public Guid? SupersedesAssertionId { get; set; }
}

internal sealed class KnowledgeSourceKindEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}

internal sealed class KnowledgeSourceKindAssertionEntity
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid SourceKindId { get; set; }
    public string AssertionOrigin { get; set; } = string.Empty;
    public string AssertedBy { get; set; } = string.Empty;
    public DateTimeOffset AssertedAt { get; set; }
    public string ReviewStatus { get; set; } = "proposed";
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Justification { get; set; }
    public Guid? SupportingSegmentId { get; set; }
    public Guid? SupersedesAssertionId { get; set; }
}

internal sealed class KnowledgePerspectiveEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public Guid? ParentPerspectiveId { get; set; }
    public string? Description { get; set; }
    public string? HistoricalPeriod { get; set; }
}

internal sealed class KnowledgePerspectiveAssertionEntity
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid PerspectiveId { get; set; }
    public string PerspectiveType { get; set; } = string.Empty;
    public string AssertionOrigin { get; set; } = string.Empty;
    public string AssertedBy { get; set; } = string.Empty;
    public DateTimeOffset AssertedAt { get; set; }
    public string ReviewStatus { get; set; } = "proposed";
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Justification { get; set; }
    public Guid? SupportingSegmentId { get; set; }
    public Guid? SupersedesAssertionId { get; set; }
}

internal sealed class KnowledgeMethodologicalFrameworkEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}
internal sealed class KnowledgeMethodologicalFrameworkAssertionEntity
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid MethodologicalFrameworkId { get; set; }
    public string ClassificationType { get; set; } = string.Empty;
    public string AssertionOrigin { get; set; } = string.Empty;
    public string AssertedBy { get; set; } = string.Empty;
    public DateTimeOffset AssertedAt { get; set; }
    public string ReviewStatus { get; set; } = "proposed";
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Justification { get; set; }
    public Guid? SupportingSegmentId { get; set; }
    public Guid? SupersedesAssertionId { get; set; }
}

internal sealed class KnowledgeEpistemicFrameworkEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}

internal sealed class KnowledgeEpistemicFrameworkAssertionEntity
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid EpistemicFrameworkId { get; set; }
    public string ClassificationType { get; set; } = string.Empty;
    public string AssertionOrigin { get; set; } = string.Empty;
    public string AssertedBy { get; set; } = string.Empty;
    public DateTimeOffset AssertedAt { get; set; }
    public string ReviewStatus { get; set; } = "proposed";
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Justification { get; set; }
    public Guid? SupportingSegmentId { get; set; }
    public Guid? SupersedesAssertionId { get; set; }
}

internal sealed class KnowledgeEvidenceRoleEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}

internal sealed class KnowledgeEvidenceRoleAssertionEntity
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid EvidenceRoleId { get; set; }
    public string AssertionOrigin { get; set; } = string.Empty;
    public string AssertedBy { get; set; } = string.Empty;
    public DateTimeOffset AssertedAt { get; set; }
    public string ReviewStatus { get; set; } = "proposed";
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Justification { get; set; }
    public Guid? SupportingSegmentId { get; set; }
    public Guid? SupersedesAssertionId { get; set; }
}
