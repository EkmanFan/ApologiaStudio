namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeHybridSearch
{
    public static KnowledgeHybridSearchResponse Fuse(
        KnowledgeVectorSearchResponse vectorResponse,
        KnowledgeLexicalSearchResponse lexicalResponse,
        int topK)
    {
        ArgumentNullException.ThrowIfNull(vectorResponse);
        ArgumentNullException.ThrowIfNull(lexicalResponse);

        if (topK is < 1 or > DeDecretisHybridSearchProfile.MaximumFusedSegmentK)
        {
            throw new KnowledgeImportException(
                $"Hybrid top K must be between 1 and {DeDecretisHybridSearchProfile.MaximumFusedSegmentK}.");
        }

        var vectorSegments = RankVectorSegments(vectorResponse.Results);
        var lexicalSegments = RankLexicalSegments(lexicalResponse.Results);
        var segmentIds = vectorSegments.Keys
            .Concat(lexicalSegments.Keys)
            .Distinct()
            .ToArray();

        var fused = new List<KnowledgeHybridSearchResult>(segmentIds.Length);
        foreach (var segmentId in segmentIds)
        {
            vectorSegments.TryGetValue(segmentId, out var vector);
            lexicalSegments.TryGetValue(segmentId, out var lexical);

            ValidateCompatibleEvidence(vector?.Result, lexical?.Result);

            var score = 0d;
            if (vector is not null)
            {
                score += ReciprocalRank(vector.Rank);
            }

            if (lexical is not null)
            {
                score += ReciprocalRank(lexical.Rank);
            }

            var representative = ChooseRepresentative(vector, lexical);
            fused.Add(
                new KnowledgeHybridSearchResult(
                    representative.SegmentId,
                    representative.SegmentOrdinal,
                    representative.SegmentLocator,
                    representative.SegmentTitle,
                    representative.SegmentText,
                    representative.WorkTitle,
                    representative.CitationLabel,
                    representative.ChunkId,
                    representative.ChunkOrdinal,
                    representative.ChunkText,
                    vector?.Rank,
                    lexical?.Rank,
                    vector?.Result.Similarity,
                    lexical?.Result.Score,
                    score));
        }

        var ordered = fused
            .OrderByDescending(result => result.ReciprocalRankFusionScore)
            .ThenBy(result => BestRank(result.VectorRank, result.LexicalRank))
            .ThenBy(result => result.SegmentOrdinal)
            .Take(topK)
            .ToArray();

        return new KnowledgeHybridSearchResponse(
            vectorResponse.Mode,
            vectorResponse.HnswIndexVerified,
            lexicalResponse.NormalizedQuery,
            ordered);
    }

    private static Dictionary<Guid, RankedVectorSegment> RankVectorSegments(
        IReadOnlyList<KnowledgeVectorSearchResult> results)
    {
        var ranked = new Dictionary<Guid, RankedVectorSegment>();
        var segmentRank = 0;

        foreach (var result in results)
        {
            if (ranked.ContainsKey(result.SegmentId))
            {
                continue;
            }

            segmentRank++;
            ranked.Add(
                result.SegmentId,
                new RankedVectorSegment(segmentRank, result));
        }

        return ranked;
    }

    private static Dictionary<Guid, RankedLexicalSegment> RankLexicalSegments(
        IReadOnlyList<KnowledgeLexicalSearchResult> results)
    {
        var ranked = new Dictionary<Guid, RankedLexicalSegment>();
        var segmentRank = 0;

        foreach (var result in results)
        {
            if (ranked.ContainsKey(result.SegmentId))
            {
                continue;
            }

            segmentRank++;
            ranked.Add(
                result.SegmentId,
                new RankedLexicalSegment(segmentRank, result));
        }

        return ranked;
    }

    private static HybridEvidence ChooseRepresentative(
        RankedVectorSegment? vector,
        RankedLexicalSegment? lexical)
    {
        if (vector is not null &&
            (lexical is null || vector.Rank <= lexical.Rank))
        {
            var result = vector.Result;
            return new HybridEvidence(
                result.SegmentId,
                result.SegmentOrdinal,
                result.SegmentLocator,
                result.SegmentTitle,
                result.SegmentText,
                result.WorkTitle,
                result.CitationLabel,
                result.ChunkId,
                result.ChunkOrdinal,
                result.ChunkText);
        }

        if (lexical is not null)
        {
            var result = lexical.Result;
            return new HybridEvidence(
                result.SegmentId,
                result.SegmentOrdinal,
                result.SegmentLocator,
                result.SegmentTitle,
                result.SegmentText,
                result.WorkTitle,
                result.CitationLabel,
                result.ChunkId,
                result.ChunkOrdinal,
                result.ChunkText);
        }

        throw new KnowledgeImportException(
            "Hybrid fusion encountered a segment with no retrieval evidence.");
    }

    private static void ValidateCompatibleEvidence(
        KnowledgeVectorSearchResult? vector,
        KnowledgeLexicalSearchResult? lexical)
    {
        if (vector is null || lexical is null)
        {
            return;
        }

        if (vector.SegmentId != lexical.SegmentId ||
            vector.SegmentOrdinal != lexical.SegmentOrdinal ||
            !string.Equals(vector.SegmentText, lexical.SegmentText, StringComparison.Ordinal) ||
            !string.Equals(vector.WorkTitle, lexical.WorkTitle, StringComparison.Ordinal) ||
            !string.Equals(vector.CitationLabel, lexical.CitationLabel, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Vector and lexical retrieval disagree on metadata for segment {vector.SegmentId}.");
        }
    }

    private static double ReciprocalRank(int rank) =>
        1d / (DeDecretisHybridSearchProfile.ReciprocalRankConstant + rank);

    private static int BestRank(int? vectorRank, int? lexicalRank) =>
        Math.Min(vectorRank ?? int.MaxValue, lexicalRank ?? int.MaxValue);

    private sealed record RankedVectorSegment(
        int Rank,
        KnowledgeVectorSearchResult Result);

    private sealed record RankedLexicalSegment(
        int Rank,
        KnowledgeLexicalSearchResult Result);

    private sealed record HybridEvidence(
        Guid SegmentId,
        int SegmentOrdinal,
        string? SegmentLocator,
        string? SegmentTitle,
        string SegmentText,
        string WorkTitle,
        string? CitationLabel,
        Guid ChunkId,
        int ChunkOrdinal,
        string ChunkText);
}
