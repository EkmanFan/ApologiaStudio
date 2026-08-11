using System.Globalization;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeLexicalSearchCli
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
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for search-lexical.");
        }

        var response = await KnowledgeLexicalSearch.SearchAsync(
            connectionString,
            options.Query,
            options.TopK,
            cancellationToken);

        WriteResult(options, response);
        return 0;
    }

    private static void WriteResult(
        SearchOptions options,
        KnowledgeLexicalSearchResponse response)
    {
        Console.WriteLine($"Search profile: {DeDecretisLexicalSearchProfile.ProfileId}");
        Console.WriteLine($"Source profile: {DeDecretisDocument.ProfileId}");
        Console.WriteLine($"Query: {options.Query}");
        Console.WriteLine(
            $"Text search configuration: {DeDecretisLexicalSearchProfile.TextSearchConfiguration}");
        Console.WriteLine($"Query strategy: {DeDecretisLexicalSearchProfile.QueryStrategy}");
        Console.WriteLine($"Normalized tsquery: {response.NormalizedQuery ?? "(none)"}");
        Console.WriteLine($"Top K: {options.TopK}");
        Console.WriteLine("RESULT: SEARCHED");
        Console.WriteLine($"Results: {response.Results.Count}");

        for (var index = 0; index < response.Results.Count; index++)
        {
            var result = response.Results[index];
            Console.WriteLine();
            Console.WriteLine(
                $"#{index + 1} lexical_score={result.Score.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Work: {result.WorkTitle}");
            Console.WriteLine($"Citation label: {result.CitationLabel ?? "(none)"}");
            Console.WriteLine(
                $"Segment: {result.SegmentLocator ?? result.SegmentTitle ?? result.SegmentId.ToString()}");
            Console.WriteLine($"Chunk ordinal: {result.ChunkOrdinal}");
            Console.WriteLine($"Chunk: {result.ChunkText.ReplaceLineEndings(" ")}");
        }
    }

    private sealed record SearchOptions(
        string Query,
        int TopK)
    {
        public static SearchOptions Parse(IReadOnlyList<string> args)
        {
            string? query = null;
            var topK = DeDecretisLexicalSearchProfile.DefaultTopK;

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
                    default:
                        throw new KnowledgeImportException(
                            $"Unknown search-lexical option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                throw new KnowledgeImportException(
                    "Missing required option --query for search-lexical.");
            }

            if (topK is < 1 or > DeDecretisLexicalSearchProfile.MaximumTopK)
            {
                throw new KnowledgeImportException(
                    $"Top K must be between 1 and {DeDecretisLexicalSearchProfile.MaximumTopK}.");
            }

            return new SearchOptions(query.Trim(), topK);
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
