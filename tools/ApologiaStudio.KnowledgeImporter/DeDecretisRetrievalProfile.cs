namespace ApologiaStudio.KnowledgeImporter;

internal static class DeDecretisRetrievalProfile
{
    public const string ProfileId = "de-decretis-retrieval-qwen3-embedding-4b-v1";
    public const string ChunkingStrategy = "segment-character-window";
    public const string ChunkingVersion = "v1";
    public const int MaxChunkCharacters = 1_800;
    public const int OverlapCharacters = 300;
    public const int BoundarySearchCharacters = 400;
    public const int MinimumPreferredChunkCharacters = 1_000;
    public const string EmbeddingProvider = "ollama";
    public const string EmbeddingModel = "qwen3-embedding:4b";
    public const int EmbeddingDimensions = 2_560;
    public const string OllamaBaseAddress = "http://127.0.0.1:11434";
}

internal sealed record PreparedRetrievalChunk(
    Guid Id,
    int Ordinal,
    Guid SegmentId,
    int SegmentNumber,
    int StartOffset,
    int EndOffset,
    string Text);
