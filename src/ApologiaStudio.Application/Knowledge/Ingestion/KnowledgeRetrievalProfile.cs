namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record KnowledgeRetrievalProfile(
    string ProfileId,
    string ChunkingStrategy,
    string ChunkingVersion,
    int MaxChunkCharacters,
    int OverlapCharacters,
    int BoundarySearchCharacters,
    int MinimumPreferredChunkCharacters,
    string EmbeddingProvider,
    string EmbeddingModel,
    int EmbeddingDimensions);
