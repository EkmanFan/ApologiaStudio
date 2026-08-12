namespace ApologiaStudio.Application.Knowledge.Ingestion;

public static class KnowledgeRetrievalChunkBuilder
{
    public static IReadOnlyList<KnowledgeRetrievalChunk> Build(
        KnowledgeImportPackage package,
        KnowledgeRetrievalProfile profile)
    {
        KnowledgeImportPackageValidator.Validate(package);
        ArgumentNullException.ThrowIfNull(profile);

        ValidateProfile(profile);

        var normalizedArtifact = package.Artifacts.Single(
            artifact => artifact.Id == package.NormalizedArtifactId);

        if (!string.Equals(
                normalizedArtifact.EditorialReviewStatus,
                "approved",
                StringComparison.Ordinal) ||
            !string.Equals(
                normalizedArtifact.LifecycleStatus,
                "active",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Retrieval chunks can only be built from an active, editorially approved normalized artifact.");
        }

        var segments = package.Segments
            .Where(segment =>
                segment.ArtifactId ==
                    package.NormalizedArtifactId &&
                string.Equals(
                    segment.EditorialReviewStatus,
                    "approved",
                    StringComparison.Ordinal) &&
                IsRetrievalEligible(segment.SegmentKind))
            .OrderBy(segment => segment.Ordinal)
            .ToArray();

        if (segments.Length == 0)
        {
            throw new InvalidOperationException(
                "The normalized artifact contains no retrieval-eligible segments.");
        }

        var chunks = new List<KnowledgeRetrievalChunk>();
        var ordinal = 0;

        foreach (var segment in segments)
        {
            var startOffset = 0;

            while (startOffset < segment.Text.Length)
            {
                var endOffset = FindEndOffset(
                    segment.Text,
                    startOffset,
                    profile);

                if (endOffset <= startOffset)
                {
                    throw new InvalidOperationException(
                        $"Chunking did not make progress in segment {segment.Ordinal} at offset {startOffset}.");
                }

                var text =
                    segment.Text[startOffset..endOffset];

                var id =
                    KnowledgeStableIds.ForSourceProfile(
                        package.StableIdNamespace,
                        $"retrieval/{profile.ProfileId}/" +
                        $"chunk/{segment.Id:D}/{startOffset}-{endOffset}");

                chunks.Add(
                    new KnowledgeRetrievalChunk(
                        id,
                        ordinal++,
                        segment.Id,
                        segment.Ordinal,
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
                    endOffset,
                    profile);
            }
        }

        ValidateChunks(
            segments,
            chunks,
            profile);

        return chunks;
    }

    private static int FindEndOffset(
        string text,
        int startOffset,
        KnowledgeRetrievalProfile profile)
    {
        var hardEnd = Math.Min(
            text.Length,
            startOffset +
            profile.MaxChunkCharacters);

        if (hardEnd == text.Length)
        {
            return hardEnd;
        }

        var minimumEnd = Math.Min(
            hardEnd,
            startOffset +
            profile.MinimumPreferredChunkCharacters);

        var searchStart = Math.Max(
            minimumEnd,
            hardEnd -
            profile.BoundarySearchCharacters);

        for (var index = hardEnd - 1;
             index >= searchStart;
             index--)
        {
            if (IsSentenceBoundary(
                    text,
                    index))
            {
                return index + 1;
            }
        }

        for (var index = hardEnd - 1;
             index >= minimumEnd;
             index--)
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
        int currentEnd,
        KnowledgeRetrievalProfile profile)
    {
        var target = Math.Max(
            currentStart + 1,
            currentEnd -
            profile.OverlapCharacters);

        const int alignmentWindow = 100;

        var lowerBound = Math.Max(
            currentStart + 1,
            target - alignmentWindow);

        for (var index = target;
             index >= lowerBound;
             index--)
        {
            if (index > 0 &&
                char.IsWhiteSpace(text[index - 1]))
            {
                return index;
            }
        }

        var upperBound = Math.Min(
            currentEnd - 1,
            target + alignmentWindow);

        for (var index = target + 1;
             index <= upperBound;
             index++)
        {
            if (index > 0 &&
                char.IsWhiteSpace(text[index - 1]))
            {
                return index;
            }
        }

        return target;
    }

    private static bool IsSentenceBoundary(
        string text,
        int index)
    {
        if (index < 0 ||
            index >= text.Length - 1)
        {
            return false;
        }

        return text[index] is
                   '.' or '?' or '!' or ';' or ':'
               && char.IsWhiteSpace(
                   text[index + 1]);
    }

    private static bool IsRetrievalEligible(
        DocumentSegmentKind kind) =>
        kind is
            DocumentSegmentKind.MainText or
            DocumentSegmentKind.Sidebar or
            DocumentSegmentKind.Caption;

    private static void ValidateChunks(
        IReadOnlyList<KnowledgeImportSegment> segments,
        IReadOnlyList<KnowledgeRetrievalChunk> chunks,
        KnowledgeRetrievalProfile profile)
    {
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException(
                "The retrieval projection produced no chunks.");
        }

        if (chunks
                .Select(chunk => chunk.Id)
                .Distinct()
                .Count() != chunks.Count)
        {
            throw new InvalidOperationException(
                "The retrieval projection produced duplicate chunk identifiers.");
        }

        if (chunks
                .Select(chunk => chunk.Ordinal)
                .Distinct()
                .Count() != chunks.Count ||
            chunks.Min(chunk => chunk.Ordinal) != 0 ||
            chunks.Max(chunk => chunk.Ordinal) !=
                chunks.Count - 1)
        {
            throw new InvalidOperationException(
                "Retrieval chunk ordinals are not a contiguous zero-based sequence.");
        }

        foreach (var segment in segments)
        {
            var segmentChunks = chunks
                .Where(chunk =>
                    chunk.SegmentId == segment.Id)
                .OrderBy(chunk =>
                    chunk.StartOffset)
                .ToArray();

            if (segmentChunks.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Segment {segment.Ordinal} has no retrieval chunks.");
            }

            if (segmentChunks[0].StartOffset != 0 ||
                segmentChunks[^1].EndOffset !=
                    segment.Text.Length)
            {
                throw new InvalidOperationException(
                    $"Retrieval chunks do not cover the full text of segment {segment.Ordinal}.");
            }

            var previousEnd = 0;

            for (var index = 0;
                 index < segmentChunks.Length;
                 index++)
            {
                var chunk = segmentChunks[index];

                if (chunk.SegmentOrdinal !=
                        segment.Ordinal ||
                    chunk.StartOffset < 0 ||
                    chunk.EndOffset <=
                        chunk.StartOffset ||
                    chunk.EndOffset >
                        segment.Text.Length ||
                    chunk.Text.Length >
                        profile.MaxChunkCharacters ||
                    !string.Equals(
                        chunk.Text,
                        segment.Text[
                            chunk.StartOffset..
                            chunk.EndOffset],
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Retrieval chunk {chunk.Ordinal} is inconsistent with segment {segment.Ordinal}.");
                }

                if (index > 0 &&
                    chunk.StartOffset > previousEnd)
                {
                    throw new InvalidOperationException(
                        $"Retrieval chunks leave a gap in segment {segment.Ordinal}.");
                }

                previousEnd = chunk.EndOffset;
            }
        }
    }

    private static void ValidateProfile(
        KnowledgeRetrievalProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profile.ProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profile.ChunkingStrategy);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profile.ChunkingVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profile.EmbeddingProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            profile.EmbeddingModel);

        if (profile.MaxChunkCharacters <= 0 ||
            profile.OverlapCharacters < 0 ||
            profile.OverlapCharacters >=
                profile.MaxChunkCharacters ||
            profile.BoundarySearchCharacters < 0 ||
            profile.MinimumPreferredChunkCharacters <= 0 ||
            profile.MinimumPreferredChunkCharacters >
                profile.MaxChunkCharacters ||
            profile.EmbeddingDimensions <= 0)
        {
            throw new ArgumentException(
                $"Retrieval profile {profile.ProfileId} has invalid numeric settings.",
                nameof(profile));
        }
    }
}
