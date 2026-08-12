namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record KnowledgeImportResult(
    bool WasCreated,
    Guid WorkId,
    Guid NormalizedArtifactId,
    int SegmentCount);
