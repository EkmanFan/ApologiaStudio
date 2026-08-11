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
