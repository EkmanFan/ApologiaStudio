namespace ApologiaStudio.KnowledgeImporter;

internal static class DeDecretisHybridSearchProfile
{
    public const string ProfileId = "de-decretis-hybrid-search-v1";
    public const string FusionStrategy = "segment-level-rrf";
    public const int ReciprocalRankConstant = 60;
    public const int DefaultTopK = 5;
    public const int DefaultCandidateChunkK = 20;
    public const int MaximumCandidateChunkK = 20;
    public const int MaximumFusedSegmentK = 40;
}

internal sealed record KnowledgeHybridSearchResult(
    Guid SegmentId,
    int SegmentOrdinal,
    string? SegmentLocator,
    string? SegmentTitle,
    string SegmentText,
    string WorkTitle,
    string? CitationLabel,
    Guid RepresentativeChunkId,
    int RepresentativeChunkOrdinal,
    string RepresentativeChunkText,
    int? VectorRank,
    int? LexicalRank,
    double? VectorSimilarity,
    double? LexicalScore,
    double ReciprocalRankFusionScore);

internal sealed record KnowledgeHybridSearchResponse(
    VectorSearchMode VectorMode,
    bool HnswIndexVerified,
    string? NormalizedLexicalQuery,
    IReadOnlyList<KnowledgeHybridSearchResult> Results);
