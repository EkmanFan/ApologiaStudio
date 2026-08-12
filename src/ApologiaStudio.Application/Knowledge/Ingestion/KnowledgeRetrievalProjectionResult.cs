namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record KnowledgeRetrievalProjectionResult(
    bool WasCreated,
    Guid NormalizedArtifactId,
    int ChunkCount,
    int EmbeddingCount,
    string ModelDigest);
