using System.Globalization;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeHybridSearchCli
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var options = HybridSearchOptions.Parse(args);
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new KnowledgeImportException(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for search-hybrid.");
        }

        using var ollama = new OllamaEmbeddingClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress));
        var modelDigest = await ollama.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        var embeddings = await ollama.EmbedAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            DeDecretisRetrievalProfile.EmbeddingDimensions,
            [DeDecretisVectorSearchProfile.FormatQuery(options.Query)],
            cancellationToken);
        var digestAfterEmbedding = await ollama.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        if (!string.Equals(modelDigest, digestAfterEmbedding, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama embedding model changed while the hybrid query was being embedded.");
        }

        var vector = await KnowledgeVectorSearch.SearchAsync(
            connectionString,
            embeddings[0],
            modelDigest,
            options.CandidateChunkK,
            options.Mode,
            cancellationToken);
        var lexical = await KnowledgeLexicalSearch.SearchAsync(
            connectionString,
            options.Query,
            options.CandidateChunkK,
            cancellationToken);
        var hybrid = KnowledgeHybridSearch.Fuse(
            vector,
            lexical,
            options.TopK);

        WriteResult(options, modelDigest, hybrid);
        return 0;
    }

    private static void WriteResult(
        HybridSearchOptions options,
        string modelDigest,
        KnowledgeHybridSearchResponse response)
    {
        Console.WriteLine($"Search profile: {DeDecretisHybridSearchProfile.ProfileId}");
        Console.WriteLine($"Vector profile: {DeDecretisVectorSearchProfile.ProfileId}");
        Console.WriteLine($"Lexical profile: {DeDecretisLexicalSearchProfile.ProfileId}");
        Console.WriteLine($"Query: {options.Query}");
        Console.WriteLine($"Vector mode: {options.Mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Fusion strategy: {DeDecretisHybridSearchProfile.FusionStrategy}");
        Console.WriteLine($"RRF constant: {DeDecretisHybridSearchProfile.ReciprocalRankConstant}");
        Console.WriteLine($"Candidate chunk K per branch: {options.CandidateChunkK}");
        Console.WriteLine($"Top K segments: {options.TopK}");
        Console.WriteLine($"Embedding model: {DeDecretisRetrievalProfile.EmbeddingModel}");
        Console.WriteLine($"Model digest: {modelDigest}");
        Console.WriteLine(
            $"Normalized lexical tsquery: {response.NormalizedLexicalQuery ?? "(none)"}");
        Console.WriteLine(
            $"HNSW index verified: {(options.Mode == VectorSearchMode.Hnsw ? (response.HnswIndexVerified ? "yes" : "no") : "not requested")}");
        Console.WriteLine("RESULT: SEARCHED");
        Console.WriteLine($"Results: {response.Results.Count}");

        for (var index = 0; index < response.Results.Count; index++)
        {
            var result = response.Results[index];
            Console.WriteLine();
            Console.WriteLine(
                $"#{index + 1} rrf={result.ReciprocalRankFusionScore.ToString("F8", CultureInfo.InvariantCulture)} " +
                $"vector_rank={FormatRank(result.VectorRank)} lexical_rank={FormatRank(result.LexicalRank)}");
            Console.WriteLine($"Work: {result.WorkTitle}");
            Console.WriteLine(
                $"Citation label: {result.CitationLabel ?? result.WorkTitle}");
            Console.WriteLine(
                $"Segment: {result.SegmentLocator ?? result.SegmentTitle ?? $"§{result.SegmentOrdinal}"}");
            Console.WriteLine(
                $"Vector similarity: {FormatScore(result.VectorSimilarity)}");
            Console.WriteLine(
                $"Lexical score: {FormatScore(result.LexicalScore)}");
            Console.WriteLine($"Representative chunk ordinal: {result.RepresentativeChunkOrdinal}");
            Console.WriteLine($"Chunk: {result.RepresentativeChunkText}");
        }
    }

    private static string FormatRank(int? rank) =>
        rank?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private static string FormatScore(double? value) =>
        value?.ToString("F6", CultureInfo.InvariantCulture) ?? "-";

    private sealed record HybridSearchOptions(
        string Query,
        int TopK,
        int CandidateChunkK,
        VectorSearchMode Mode)
    {
        public static HybridSearchOptions Parse(IReadOnlyList<string> args)
        {
            string? query = null;
            var topK = DeDecretisHybridSearchProfile.DefaultTopK;
            var candidateChunkK = DeDecretisHybridSearchProfile.DefaultCandidateChunkK;
            var mode = VectorSearchMode.Exact;

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--query":
                        query = ReadValue(args, ref index, "--query");
                        break;
                    case "--top-k":
                        topK = ParsePositiveInt(
                            ReadValue(args, ref index, "--top-k"),
                            "--top-k");
                        break;
                    case "--candidate-k":
                        candidateChunkK = ParsePositiveInt(
                            ReadValue(args, ref index, "--candidate-k"),
                            "--candidate-k");
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
                    default:
                        throw new KnowledgeImportException(
                            $"Unknown search-hybrid option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new KnowledgeImportException(
                    "Missing required option --query for search-hybrid.");
            }

            if (topK > DeDecretisHybridSearchProfile.MaximumFusedSegmentK)
            {
                throw new KnowledgeImportException(
                    $"--top-k cannot exceed {DeDecretisHybridSearchProfile.MaximumFusedSegmentK}.");
            }

            if (candidateChunkK > DeDecretisHybridSearchProfile.MaximumCandidateChunkK)
            {
                throw new KnowledgeImportException(
                    $"--candidate-k cannot exceed {DeDecretisHybridSearchProfile.MaximumCandidateChunkK}.");
            }

            if (topK > candidateChunkK * 2)
            {
                throw new KnowledgeImportException(
                    "--top-k cannot exceed twice --candidate-k because the fused segment pool comes from two branches.");
            }

            return new HybridSearchOptions(
                query.Trim(),
                topK,
                candidateChunkK,
                mode);
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
            if (index >= args.Count ||
                args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new KnowledgeImportException(
                    $"Missing value for {option}.");
            }

            return args[index];
        }
    }
}
