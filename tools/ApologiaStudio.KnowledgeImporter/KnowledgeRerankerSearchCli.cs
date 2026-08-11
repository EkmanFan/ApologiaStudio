using System.Globalization;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeRerankerSearchCli
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var options = SearchOptions.Parse(args);
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new KnowledgeImportException(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for rerank-retrieval.");
        }

        using var embeddingClient = new OllamaEmbeddingClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress));
        var embeddingDigest = await embeddingClient.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        var embedding = await embeddingClient.EmbedAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            DeDecretisRetrievalProfile.EmbeddingDimensions,
            new[] { DeDecretisVectorSearchProfile.FormatQuery(options.Query) },
            cancellationToken);
        var embeddingDigestAfter = await embeddingClient.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        if (!string.Equals(embeddingDigest, embeddingDigestAfter, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama embedding model changed while the reranker query was being embedded.");
        }

        var vector = await KnowledgeVectorSearch.SearchAsync(
            connectionString,
            embedding.Single(),
            embeddingDigest,
            DeDecretisRerankerProfile.CandidateChunkK,
            options.Mode,
            cancellationToken);
        var candidates = KnowledgeReranker.BuildCandidates(
            vector,
            options.CandidateSegmentK);

        using var rerankerClient = new OllamaListwiseRerankerClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress),
            TimeSpan.FromSeconds(DeDecretisRerankerProfile.TimeoutSeconds));
        var rerankerDigest = await rerankerClient.ResolveModelDigestAsync(
            DeDecretisRerankerProfile.RerankerModel,
            cancellationToken);
        var rerankResult = await rerankerClient.RerankAsync(
            DeDecretisRerankerProfile.RerankerModel,
            options.Query,
            candidates,
            cancellationToken);
        var rerankerDigestAfter = await rerankerClient.ResolveModelDigestAsync(
            DeDecretisRerankerProfile.RerankerModel,
            cancellationToken);
        if (!string.Equals(rerankerDigest, rerankerDigestAfter, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama reranker model changed during listwise reranking.");
        }

        var reranked = KnowledgeReranker.ApplyOrdering(
            candidates,
            rerankResult.OrderedIds);
        WriteResult(
            options,
            embeddingDigest,
            rerankerDigest,
            vector.HnswIndexVerified,
            rerankResult,
            reranked);
        return 0;
    }

    private static void WriteResult(
        SearchOptions options,
        string embeddingDigest,
        string rerankerDigest,
        bool hnswIndexVerified,
        OllamaListwiseRerankResult rerankResult,
        IReadOnlyList<RerankedSegment> reranked)
    {
        Console.WriteLine($"Reranker profile: {DeDecretisRerankerProfile.ProfileId}");
        Console.WriteLine($"Vector profile: {DeDecretisVectorSearchProfile.ProfileId}");
        Console.WriteLine($"Query: {options.Query}");
        Console.WriteLine($"Vector mode: {options.Mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Candidate chunks: {DeDecretisRerankerProfile.CandidateChunkK}");
        Console.WriteLine($"Candidate segments: {reranked.Count}");
        Console.WriteLine($"Top K: {options.TopK}");
        Console.WriteLine($"Reranker kind: {DeDecretisRerankerProfile.RerankerKind}");
        Console.WriteLine($"Embedding model: {DeDecretisRetrievalProfile.EmbeddingModel}");
        Console.WriteLine($"Embedding digest: {embeddingDigest}");
        Console.WriteLine($"Reranker model: {DeDecretisRerankerProfile.RerankerModel}");
        Console.WriteLine($"Reranker digest: {rerankerDigest}");
        Console.WriteLine(
            $"HNSW index verified: {(options.Mode == VectorSearchMode.Hnsw ? (hnswIndexVerified ? "yes" : "no") : "not requested")}");
        Console.WriteLine(
            $"Reranker total ms: {ToMilliseconds(rerankResult.TotalDurationNanoseconds).ToString("F1", CultureInfo.InvariantCulture)}");
        Console.WriteLine("RESULT: RERANKED");
        Console.WriteLine($"Results: {Math.Min(options.TopK, reranked.Count)}");

        foreach (var result in reranked.Take(options.TopK))
        {
            var evidence = result.Evidence;
            Console.WriteLine();
            Console.WriteLine(
                $"#{result.RerankRank} vector_rank={result.VectorRank} similarity={evidence.Similarity.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Work: {evidence.WorkTitle}");
            Console.WriteLine($"Citation label: {evidence.CitationLabel ?? "(none)"}");
            Console.WriteLine(
                $"Segment: {evidence.SegmentLocator ?? $"§{evidence.SegmentOrdinal}"}");
            Console.WriteLine($"Representative chunk ordinal: {evidence.ChunkOrdinal}");
            Console.WriteLine($"Chunk: {evidence.ChunkText}");
        }
    }

    private static double ToMilliseconds(long? nanoseconds) =>
        nanoseconds is null ? 0d : nanoseconds.Value / 1_000_000d;

    private sealed record SearchOptions(
        string Query,
        VectorSearchMode Mode,
        int TopK,
        int CandidateSegmentK)
    {
        public static SearchOptions Parse(IReadOnlyList<string> args)
        {
            string? query = null;
            var mode = VectorSearchMode.Exact;
            var topK = DeDecretisRerankerProfile.DefaultTopK;
            var candidateSegmentK = DeDecretisRerankerProfile.CandidateSegmentK;
            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--query":
                        query = ReadValue(args, ref index, "--query");
                        break;
                    case "--mode":
                        mode = ReadValue(args, ref index, "--mode") switch
                        {
                            "exact" => VectorSearchMode.Exact,
                            "hnsw" => VectorSearchMode.Hnsw,
                            var value => throw new KnowledgeImportException(
                                $"Invalid --mode value '{value}'. Expected exact or hnsw.")
                        };
                        break;
                    case "--top-k":
                        topK = ParsePositiveInt(ReadValue(args, ref index, "--top-k"), "--top-k");
                        break;
                    case "--candidate-segment-k":
                        candidateSegmentK = ParsePositiveInt(
                            ReadValue(args, ref index, "--candidate-segment-k"),
                            "--candidate-segment-k");
                        break;
                    default:
                        throw new KnowledgeImportException(
                            $"Unknown rerank-retrieval option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new KnowledgeImportException(
                    "Missing required option --query for rerank-retrieval.");
            }

            if (candidateSegmentK > DeDecretisRerankerProfile.MaximumTopK)
            {
                throw new KnowledgeImportException(
                    $"--candidate-segment-k cannot exceed {DeDecretisRerankerProfile.MaximumTopK}.");
            }

            if (topK > candidateSegmentK)
            {
                throw new KnowledgeImportException(
                    "--top-k cannot be greater than --candidate-segment-k.");
            }

            return new SearchOptions(query.Trim(), mode, topK, candidateSegmentK);
        }

        private static int ParsePositiveInt(string text, string option)
        {
            if (!int.TryParse(
                    text,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value) ||
                value < 1)
            {
                throw new KnowledgeImportException(
                    $"Invalid {option} value '{text}'. Expected a positive integer.");
            }

            return value;
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string option)
        {
            index++;
            if (index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new KnowledgeImportException($"Missing value for {option}.");
            }

            return args[index];
        }
    }
}
