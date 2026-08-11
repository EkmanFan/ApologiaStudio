using System.Globalization;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeSearchCli
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
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for search-retrieval.");
        }

        using var ollama = new OllamaEmbeddingClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress));
        var modelDigest = await ollama.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);

        var instructedQuery = DeDecretisVectorSearchProfile.FormatQuery(options.Query);
        var embeddings = await ollama.EmbedAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            DeDecretisRetrievalProfile.EmbeddingDimensions,
            [instructedQuery],
            cancellationToken);

        var digestAfterEmbedding = await ollama.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        if (!string.Equals(modelDigest, digestAfterEmbedding, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama embedding model changed while the search query was being embedded.");
        }

        var response = await KnowledgeVectorSearch.SearchAsync(
            connectionString,
            embeddings[0],
            modelDigest,
            options.TopK,
            options.Mode,
            cancellationToken);

        WriteResult(options, modelDigest, response);
        return 0;
    }

    private static void WriteResult(
        SearchOptions options,
        string modelDigest,
        KnowledgeVectorSearchResponse response)
    {
        Console.WriteLine($"Search profile: {DeDecretisVectorSearchProfile.ProfileId}");
        Console.WriteLine($"Retrieval profile: {DeDecretisRetrievalProfile.ProfileId}");
        Console.WriteLine($"Query: {options.Query}");
        Console.WriteLine($"Mode: {options.Mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Top K: {options.TopK}");
        Console.WriteLine($"Embedding model: {DeDecretisRetrievalProfile.EmbeddingModel}");
        Console.WriteLine($"Model digest: {modelDigest}");
        Console.WriteLine($"HNSW index verified: {(response.HnswIndexVerified ? "yes" : "not requested")}");
        Console.WriteLine("RESULT: SEARCHED");
        Console.WriteLine($"Results: {response.Results.Count}");

        for (var index = 0; index < response.Results.Count; index++)
        {
            var result = response.Results[index];
            Console.WriteLine();
            Console.WriteLine(
                $"#{index + 1} similarity={result.Similarity.ToString("F6", CultureInfo.InvariantCulture)} " +
                $"distance={result.Distance.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Work: {result.WorkTitle}");
            Console.WriteLine($"Citation label: {result.CitationLabel ?? "(none)"}");
            Console.WriteLine($"Segment: {result.SegmentLocator ?? result.SegmentTitle ?? result.SegmentId.ToString()}");
            Console.WriteLine($"Chunk ordinal: {result.ChunkOrdinal}");
            Console.WriteLine($"Chunk: {result.ChunkText.ReplaceLineEndings(" ")}");
        }
    }

    private sealed record SearchOptions(
        string Query,
        int TopK,
        VectorSearchMode Mode)
    {
        public static SearchOptions Parse(IReadOnlyList<string> args)
        {
            string? query = null;
            var topK = DeDecretisVectorSearchProfile.DefaultTopK;
            var mode = VectorSearchMode.Exact;

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--query":
                        query = ReadValue(args, ref index, "--query");
                        break;
                    case "--top-k":
                        var topKText = ReadValue(args, ref index, "--top-k");
                        if (!int.TryParse(
                                topKText,
                                NumberStyles.None,
                                CultureInfo.InvariantCulture,
                                out topK))
                        {
                            throw new KnowledgeImportException(
                                $"Invalid --top-k value '{topKText}'.");
                        }

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
                            $"Unknown search-retrieval option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new KnowledgeImportException(
                    "Missing required option --query for search-retrieval.");
            }

            if (topK is < 1 or > DeDecretisVectorSearchProfile.MaximumTopK)
            {
                throw new KnowledgeImportException(
                    $"Top K must be between 1 and {DeDecretisVectorSearchProfile.MaximumTopK}.");
            }

            return new SearchOptions(query.Trim(), topK, mode);
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
