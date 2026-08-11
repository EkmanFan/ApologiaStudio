namespace ApologiaStudio.KnowledgeImporter;

internal static class DeDecretisVectorSearchProfile
{
    public const string ProfileId = "de-decretis-vector-search-v1";
    public const string QueryTask =
        "Given a user question about approved historical and theological sources, retrieve passages that provide evidence relevant to answering the question.";
    public const int DefaultTopK = 5;
    public const int MaximumTopK = 20;
    public const int HnswEfSearch = 100;
    public const string HnswIndexName =
        "ix_knowledge_chunk_embeddings_qwen3_4b_hnsw_cosine";

    public static string FormatQuery(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return $"Instruct: {QueryTask}\nQuery: {query.Trim()}";
    }
}

internal enum VectorSearchMode
{
    Exact,
    Hnsw
}

internal sealed record KnowledgeVectorSearchResult(
    Guid ChunkId,
    int ChunkOrdinal,
    string ChunkText,
    Guid SegmentId,
    int SegmentOrdinal,
    string? SegmentLocator,
    string? SegmentTitle,
    string SegmentText,
    int StartOffset,
    int EndOffset,
    string WorkTitle,
    string? CitationLabel,
    double Distance)
{
    public double Similarity => 1d - Distance;
}

internal sealed record KnowledgeVectorSearchResponse(
    VectorSearchMode Mode,
    bool HnswIndexVerified,
    IReadOnlyList<KnowledgeVectorSearchResult> Results);
