namespace ApologiaStudio.KnowledgeImporter;

internal static class DeDecretisLexicalSearchProfile
{
    public const string ProfileId = "de-decretis-lexical-search-v1";
    public const string TextSearchConfiguration = "english";
    public const string QueryStrategy = "normalized-lexeme-or";
    public const string NormalizedArtifactSha256 =
        "96e8b0deacf6fabe286f50e6d3d79be44fe5d3382c06bb34ff11d0681edf1452";
    public const int RankNormalization = 32;
    public const int DefaultTopK = 5;
    public const int MaximumTopK = 20;
}

internal sealed record KnowledgeLexicalSearchResult(
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
    double Score);

internal sealed record KnowledgeLexicalSearchResponse(
    string? NormalizedQuery,
    IReadOnlyList<KnowledgeLexicalSearchResult> Results);
