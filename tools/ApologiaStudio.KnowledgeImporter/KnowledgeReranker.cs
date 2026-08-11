namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeReranker
{
    public static IReadOnlyList<RerankerCandidate> BuildCandidates(
        KnowledgeVectorSearchResponse vectorResponse,
        int candidateSegmentK)
    {
        ArgumentNullException.ThrowIfNull(vectorResponse);
        if (candidateSegmentK is < 1 or > DeDecretisRerankerProfile.MaximumTopK)
        {
            throw new KnowledgeImportException(
                $"Candidate segment K must be between 1 and {DeDecretisRerankerProfile.MaximumTopK}.");
        }

        var candidates = new List<RerankerCandidate>(candidateSegmentK);
        var seenSegments = new HashSet<Guid>();
        var segmentRank = 0;
        foreach (var result in vectorResponse.Results)
        {
            if (!seenSegments.Add(result.SegmentId))
            {
                continue;
            }

            segmentRank++;
            candidates.Add(new RerankerCandidate(
                $"C{segmentRank:D2}",
                segmentRank,
                result));
            if (candidates.Count == candidateSegmentK)
            {
                break;
            }
        }

        if (candidates.Count == 0)
        {
            throw new KnowledgeImportException(
                "Vector retrieval produced no distinct citable segments for reranking.");
        }

        return candidates;
    }

    public static IReadOnlyList<RerankedSegment> ApplyOrdering(
        IReadOnlyList<RerankerCandidate> candidates,
        IReadOnlyList<string> orderedIds)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(orderedIds);

        if (candidates.Count != orderedIds.Count)
        {
            throw new KnowledgeImportException(
                "The reranker did not return exactly one ordering entry per candidate segment.");
        }

        var byId = candidates.ToDictionary(
            candidate => candidate.CandidateId,
            StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reranked = new List<RerankedSegment>(orderedIds.Count);
        for (var index = 0; index < orderedIds.Count; index++)
        {
            var candidateId = orderedIds[index];
            if (!seen.Add(candidateId) || !byId.TryGetValue(candidateId, out var candidate))
            {
                throw new KnowledgeImportException(
                    $"The reranker returned invalid or duplicate candidate id '{candidateId}'.");
            }

            reranked.Add(new RerankedSegment(
                index + 1,
                candidate.VectorRank,
                candidate.Evidence));
        }

        if (seen.Count != candidates.Count)
        {
            throw new KnowledgeImportException(
                "The reranker omitted one or more candidate segments.");
        }

        return reranked;
    }
}
