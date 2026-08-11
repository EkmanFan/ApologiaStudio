namespace ApologiaStudio.KnowledgeImporter;

internal static class RetrievalChunkBuilder
{
    public static IReadOnlyList<PreparedRetrievalChunk> Build(
        PreparedDeDecretis prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);

        var chunks = new List<PreparedRetrievalChunk>();
        var ordinal = 0;

        foreach (var segment in prepared.Segments.OrderBy(x => x.Number))
        {
            var startOffset = 0;

            while (startOffset < segment.Text.Length)
            {
                var endOffset = FindEndOffset(segment.Text, startOffset);
                if (endOffset <= startOffset)
                {
                    throw new KnowledgeImportException(
                        $"Chunking did not make progress in section §{segment.Number} at offset {startOffset}.");
                }

                var text = segment.Text[startOffset..endOffset];
                var id = StableKnowledgeIds.ForProfile(
                    $"retrieval/{DeDecretisRetrievalProfile.ProfileId}/" +
                    $"chunk/{segment.Id:D}/{startOffset}-{endOffset}");

                chunks.Add(new PreparedRetrievalChunk(
                    id,
                    ordinal++,
                    segment.Id,
                    segment.Number,
                    startOffset,
                    endOffset,
                    text));

                if (endOffset == segment.Text.Length)
                {
                    break;
                }

                startOffset = FindNextStartOffset(
                    segment.Text,
                    startOffset,
                    endOffset);
            }
        }

        Validate(prepared, chunks);
        return chunks;
    }

    private static int FindEndOffset(string text, int startOffset)
    {
        var hardEnd = Math.Min(
            text.Length,
            startOffset + DeDecretisRetrievalProfile.MaxChunkCharacters);

        if (hardEnd == text.Length)
        {
            return hardEnd;
        }

        var minimumEnd = Math.Min(
            hardEnd,
            startOffset + DeDecretisRetrievalProfile.MinimumPreferredChunkCharacters);
        var searchStart = Math.Max(
            minimumEnd,
            hardEnd - DeDecretisRetrievalProfile.BoundarySearchCharacters);

        for (var index = hardEnd - 1; index >= searchStart; index--)
        {
            if (IsSentenceBoundary(text, index))
            {
                return index + 1;
            }
        }

        for (var index = hardEnd - 1; index >= minimumEnd; index--)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index;
            }
        }

        return hardEnd;
    }

    private static int FindNextStartOffset(
        string text,
        int currentStart,
        int currentEnd)
    {
        var target = Math.Max(
            currentStart + 1,
            currentEnd - DeDecretisRetrievalProfile.OverlapCharacters);

        const int alignmentWindow = 100;
        var lowerBound = Math.Max(currentStart + 1, target - alignmentWindow);
        for (var index = target; index >= lowerBound; index--)
        {
            if (index > 0 && char.IsWhiteSpace(text[index - 1]))
            {
                return index;
            }
        }

        var upperBound = Math.Min(currentEnd - 1, target + alignmentWindow);
        for (var index = target + 1; index <= upperBound; index++)
        {
            if (index > 0 && char.IsWhiteSpace(text[index - 1]))
            {
                return index;
            }
        }

        return target;
    }

    private static bool IsSentenceBoundary(string text, int index)
    {
        if (index < 0 || index >= text.Length - 1)
        {
            return false;
        }

        return text[index] is '.' or '?' or '!' or ';' or ':'
            && char.IsWhiteSpace(text[index + 1]);
    }

    private static void Validate(
        PreparedDeDecretis prepared,
        IReadOnlyList<PreparedRetrievalChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            throw new KnowledgeImportException(
                "The retrieval projection produced no chunks.");
        }

        if (chunks.Select(x => x.Id).Distinct().Count() != chunks.Count)
        {
            throw new KnowledgeImportException(
                "The retrieval projection produced duplicate chunk identifiers.");
        }

        if (chunks.Select(x => x.Ordinal).Distinct().Count() != chunks.Count ||
            chunks.Min(x => x.Ordinal) != 0 ||
            chunks.Max(x => x.Ordinal) != chunks.Count - 1)
        {
            throw new KnowledgeImportException(
                "Retrieval chunk ordinals are not a contiguous zero-based sequence.");
        }

        foreach (var segment in prepared.Segments)
        {
            var segmentChunks = chunks
                .Where(x => x.SegmentId == segment.Id)
                .OrderBy(x => x.StartOffset)
                .ToArray();

            if (segmentChunks.Length == 0)
            {
                throw new KnowledgeImportException(
                    $"Section §{segment.Number} has no retrieval chunks.");
            }

            if (segmentChunks[0].StartOffset != 0 ||
                segmentChunks[^1].EndOffset != segment.Text.Length)
            {
                throw new KnowledgeImportException(
                    $"Retrieval chunks do not cover the full text of section §{segment.Number}.");
            }

            var previousEnd = 0;
            for (var index = 0; index < segmentChunks.Length; index++)
            {
                var chunk = segmentChunks[index];

                if (chunk.SegmentNumber != segment.Number ||
                    chunk.StartOffset < 0 ||
                    chunk.EndOffset <= chunk.StartOffset ||
                    chunk.EndOffset > segment.Text.Length ||
                    chunk.Text.Length > DeDecretisRetrievalProfile.MaxChunkCharacters ||
                    !string.Equals(
                        chunk.Text,
                        segment.Text[chunk.StartOffset..chunk.EndOffset],
                        StringComparison.Ordinal))
                {
                    throw new KnowledgeImportException(
                        $"Invalid retrieval chunk {chunk.Ordinal} for section §{segment.Number}.");
                }

                if (index > 0 && chunk.StartOffset > previousEnd)
                {
                    throw new KnowledgeImportException(
                        $"Retrieval chunks contain a gap in section §{segment.Number}.");
                }

                previousEnd = Math.Max(previousEnd, chunk.EndOffset);
            }
        }
    }
}
