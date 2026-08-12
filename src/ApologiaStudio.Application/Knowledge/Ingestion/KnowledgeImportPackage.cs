namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record KnowledgeImportPackage(
    string ProfileId,
    string StableIdNamespace,
    Guid PrimaryWorkId,
    Guid NormalizedArtifactId,
    string EditorialActor,
    IReadOnlyList<KnowledgeImportWork> Works,
    IReadOnlyList<KnowledgeImportExpression> Expressions,
    IReadOnlyList<KnowledgeImportExpressionRelation> ExpressionRelations,
    IReadOnlyList<KnowledgeImportManifestation> Manifestations,
    IReadOnlyList<KnowledgeImportManifestationIdentifier> ManifestationIdentifiers,
    IReadOnlyList<KnowledgeImportContributor> Contributors,
    IReadOnlyList<KnowledgeImportContribution> Contributions,
    IReadOnlyList<KnowledgeImportArtifact> Artifacts,
    IReadOnlyList<KnowledgeImportProcessingActivity> ProcessingActivities,
    IReadOnlyList<KnowledgeImportSegment> Segments,
    IReadOnlyList<KnowledgeImportClassificationTerm> ClassificationTerms,
    IReadOnlyList<KnowledgeImportClassificationAssertion> ClassificationAssertions,
    IReadOnlyList<KnowledgeImportMetadataAssertion> MetadataAssertions);

public sealed record KnowledgeImportWork(
    Guid Id,
    string EditorialReviewStatus,
    string Title,
    string? OriginalLanguage,
    string? Description);

public sealed record KnowledgeImportExpression(
    Guid Id,
    string EditorialReviewStatus,
    Guid WorkId,
    string LanguageCode,
    string? Label,
    string? Description);

public sealed record KnowledgeImportExpressionRelation(
    Guid FromExpressionId,
    Guid ToExpressionId,
    string RelationType);

public sealed record KnowledgeImportManifestation(
    Guid Id,
    string EditorialReviewStatus,
    Guid ExpressionId,
    string? EditionStatement,
    int? PublicationYear,
    string? PublicationPlace,
    string? CitationLabel);

public sealed record KnowledgeImportManifestationIdentifier(
    Guid ManifestationId,
    string Scheme,
    string Value,
    string? Uri);

public sealed record KnowledgeImportContributor(
    Guid Id,
    string EditorialReviewStatus,
    string ContributorType,
    string PreferredName,
    string? SortName,
    string? Description);

public sealed record KnowledgeImportContribution(
    Guid ContributorId,
    Guid? WorkId,
    Guid? ExpressionId,
    Guid? ManifestationId,
    string Role,
    string AttributionStatus,
    int Ordinal);

public sealed record KnowledgeImportArtifact(
    Guid Id,
    string EditorialReviewStatus,
    Guid ManifestationId,
    Guid? DerivedFromArtifactId,
    string ArtifactType,
    string Sha256,
    string MediaType,
    long ByteLength,
    string? OriginUri,
    string LifecycleStatus,
    string FileExtension,
    string? SourcePath,
    byte[]? Bytes);

public sealed record KnowledgeImportProcessingActivity(
    Guid? InputArtifactId,
    Guid OutputArtifactId,
    string ActivityType,
    string ToolName,
    string ToolVersion,
    string? ConfigurationJson,
    string? ExecutedBy,
    string Status);

public sealed record KnowledgeImportSegment(
    Guid Id,
    string EditorialReviewStatus,
    Guid ArtifactId,
    Guid? ParentSegmentId,
    DocumentSegmentType SegmentType,
    DocumentSegmentKind SegmentKind,
    int Ordinal,
    string? Title,
    string Text,
    string? Locator);

public enum KnowledgeClassificationDimension
{
    SourceKind = 0,
    Perspective = 1,
    MethodologicalFramework = 2,
    EpistemicFramework = 3,
    EvidenceRole = 4
}

public sealed record KnowledgeImportClassificationTerm(
    KnowledgeClassificationDimension Dimension,
    string Code,
    string Label,
    string? Description,
    string? HistoricalPeriod);

public sealed record KnowledgeImportClassificationAssertion(
    Guid Id,
    Guid ResourceId,
    KnowledgeClassificationDimension Dimension,
    string TermCode,
    string? ClassificationType,
    string AssertionOrigin,
    string AssertedBy,
    string ReviewStatus,
    string? ReviewedBy,
    string? Justification,
    Guid? SupportingSegmentId,
    Guid? SupersedesAssertionId);

public sealed record KnowledgeImportMetadataAssertion(
    Guid Id,
    Guid ResourceId,
    string Property,
    string Value,
    string AssertionOrigin,
    string AssertedBy,
    string ReviewStatus,
    string? ReviewedBy,
    double? Confidence,
    string? Justification,
    Guid? SupportingSegmentId,
    Guid? SupersedesAssertionId);
