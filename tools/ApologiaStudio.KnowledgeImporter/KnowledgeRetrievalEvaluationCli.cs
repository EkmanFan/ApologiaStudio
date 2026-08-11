using System.Globalization;
using System.Text.Json;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeRetrievalEvaluationCli
{
    private const string EvaluationProfileId = "de-decretis-retrieval-evaluation-v1";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var options = EvaluationOptions.Parse(args);
        var dataset = await LoadDatasetAsync(
            options.DatasetPath,
            cancellationToken);
        ValidateDataset(dataset);

        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new KnowledgeImportException(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for evaluate-retrieval.");
        }

        using var ollama = new OllamaEmbeddingClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress));
        var modelDigest = await ollama.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);

        var inputs = dataset.Cases
            .Select(testCase => DeDecretisVectorSearchProfile.FormatQuery(testCase.Query))
            .ToArray();
        var embeddings = await ollama.EmbedAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            DeDecretisRetrievalProfile.EmbeddingDimensions,
            inputs,
            cancellationToken);

        var digestAfterEmbedding = await ollama.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        if (!string.Equals(
                modelDigest,
                digestAfterEmbedding,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama embedding model changed while the retrieval evaluation queries were being embedded.");
        }

        var evaluations = new List<RetrievalCaseEvaluation>(dataset.Cases.Length);
        var hnswIndexVerified = true;

        for (var index = 0; index < dataset.Cases.Length; index++)
        {
            var testCase = dataset.Cases[index];
            var response = await KnowledgeVectorSearch.SearchAsync(
                connectionString,
                embeddings[index],
                modelDigest,
                options.CandidateK,
                options.Mode,
                cancellationToken);

            if (options.Mode == VectorSearchMode.Hnsw)
            {
                hnswIndexVerified &= response.HnswIndexVerified;
            }

            evaluations.Add(EvaluateCase(
                testCase,
                response.Results,
                options.RecallK));
        }

        WriteResult(
            options,
            dataset,
            modelDigest,
            hnswIndexVerified,
            evaluations);

        return 0;
    }

    private static async Task<RetrievalEvaluationDataset> LoadDatasetAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new KnowledgeImportException(
                $"Retrieval evaluation dataset was not found: {path}");
        }

        await using var stream = File.OpenRead(path);
        var dataset = await JsonSerializer.DeserializeAsync<RetrievalEvaluationDataset>(
            stream,
            JsonOptions,
            cancellationToken);

        return dataset ?? throw new KnowledgeImportException(
            "Retrieval evaluation dataset is empty or invalid JSON.");
    }

    private static void ValidateDataset(RetrievalEvaluationDataset dataset)
    {
        if (!string.Equals(
                dataset.Profile,
                EvaluationProfileId,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Evaluation dataset profile must be '{EvaluationProfileId}'.");
        }

        if (!string.Equals(
                dataset.SourceProfile,
                DeDecretisDocument.ProfileId,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Evaluation dataset source profile must be '{DeDecretisDocument.ProfileId}'.");
        }

        if (!string.Equals(
                dataset.SearchProfile,
                DeDecretisVectorSearchProfile.ProfileId,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Evaluation dataset search profile must be '{DeDecretisVectorSearchProfile.ProfileId}'.");
        }

        if (dataset.Cases is not { Length: >= 1 })
        {
            throw new KnowledgeImportException(
                "Retrieval evaluation dataset must contain at least one case.");
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in dataset.Cases)
        {
            if (string.IsNullOrWhiteSpace(testCase.Id) ||
                !identifiers.Add(testCase.Id))
            {
                throw new KnowledgeImportException(
                    "Every retrieval evaluation case must have a non-empty unique id.");
            }

            if (string.IsNullOrWhiteSpace(testCase.Query))
            {
                throw new KnowledgeImportException(
                    $"Evaluation case '{testCase.Id}' has an empty query.");
            }

            if (testCase.Language is not ("en" or "fr"))
            {
                throw new KnowledgeImportException(
                    $"Evaluation case '{testCase.Id}' has unsupported language '{testCase.Language}'.");
            }

            if (testCase.RelevantSegments is not { Length: >= 1 })
            {
                throw new KnowledgeImportException(
                    $"Evaluation case '{testCase.Id}' has no relevant segments.");
            }

            if (testCase.RelevantSegments.Any(segment => segment is < 1 or > 32) ||
                testCase.RelevantSegments.Distinct().Count() != testCase.RelevantSegments.Length)
            {
                throw new KnowledgeImportException(
                    $"Evaluation case '{testCase.Id}' contains invalid or duplicate segment ordinals.");
            }
        }
    }

    private static RetrievalCaseEvaluation EvaluateCase(
        RetrievalEvaluationCase testCase,
        IReadOnlyList<KnowledgeVectorSearchResult> chunkResults,
        int recallK)
    {
        var rankedSegments = new List<int>();
        var seenSegments = new HashSet<int>();

        foreach (var result in chunkResults)
        {
            if (seenSegments.Add(result.SegmentOrdinal))
            {
                rankedSegments.Add(result.SegmentOrdinal);
            }
        }

        var relevant = testCase.RelevantSegments.ToHashSet();
        var topSegments = rankedSegments.Take(recallK).ToArray();
        var relevantInTopK = topSegments.Count(relevant.Contains);
        var recall = (double)relevantInTopK / relevant.Count;

        var firstRelevantRank = 0;
        for (var index = 0; index < rankedSegments.Count; index++)
        {
            if (!relevant.Contains(rankedSegments[index]))
            {
                continue;
            }

            firstRelevantRank = index + 1;
            break;
        }

        var reciprocalRank = firstRelevantRank == 0
            ? 0d
            : 1d / firstRelevantRank;

        return new RetrievalCaseEvaluation(
            testCase,
            rankedSegments,
            topSegments,
            recall,
            reciprocalRank,
            firstRelevantRank);
    }

    private static void WriteResult(
        EvaluationOptions options,
        RetrievalEvaluationDataset dataset,
        string modelDigest,
        bool hnswIndexVerified,
        IReadOnlyList<RetrievalCaseEvaluation> evaluations)
    {
        var recall = evaluations.Average(x => x.RecallAtK);
        var mrr = evaluations.Average(x => x.ReciprocalRank);
        var hitRate = evaluations.Count(x => x.FirstRelevantRank is > 0 &&
                                            x.FirstRelevantRank <= options.RecallK) /
                      (double)evaluations.Count;

        Console.WriteLine($"Evaluation profile: {EvaluationProfileId}");
        Console.WriteLine($"Dataset: {options.DatasetPath}");
        Console.WriteLine($"Source profile: {dataset.SourceProfile}");
        Console.WriteLine($"Search profile: {dataset.SearchProfile}");
        Console.WriteLine($"Retrieval profile: {DeDecretisRetrievalProfile.ProfileId}");
        Console.WriteLine($"Mode: {options.Mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Recall K: {options.RecallK}");
        Console.WriteLine($"Candidate chunk K: {options.CandidateK}");
        Console.WriteLine($"Cases: {evaluations.Count}");
        Console.WriteLine($"Embedding model: {DeDecretisRetrievalProfile.EmbeddingModel}");
        Console.WriteLine($"Model digest: {modelDigest}");
        Console.WriteLine(
            $"HNSW index verified: {(options.Mode == VectorSearchMode.Hnsw ? (hnswIndexVerified ? "yes" : "no") : "not requested")}");
        Console.WriteLine("RESULT: EVALUATED");

        foreach (var evaluation in evaluations)
        {
            Console.WriteLine();
            Console.WriteLine($"CASE: {evaluation.TestCase.Id}");
            Console.WriteLine($"Language: {evaluation.TestCase.Language}");
            Console.WriteLine($"Query: {evaluation.TestCase.Query}");
            Console.WriteLine(
                $"Relevant: {FormatSegments(evaluation.TestCase.RelevantSegments)}");
            Console.WriteLine(
                $"Retrieved@{options.RecallK}: {FormatSegments(evaluation.TopSegments)}");
            Console.WriteLine(
                $"Recall@{options.RecallK}: {evaluation.RecallAtK.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine(
                $"Reciprocal rank: {evaluation.ReciprocalRank.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine(
                $"First relevant rank: {(evaluation.FirstRelevantRank == 0 ? "none" : evaluation.FirstRelevantRank.ToString(CultureInfo.InvariantCulture))}");
        }

        Console.WriteLine();
        Console.WriteLine($"CASES: {evaluations.Count}");
        Console.WriteLine(
            $"METRIC Recall@{options.RecallK}={recall.ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"METRIC MRR={mrr.ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"METRIC HitRate@{options.RecallK}={hitRate.ToString("F6", CultureInfo.InvariantCulture)}");
    }

    private static string FormatSegments(IEnumerable<int> segments)
    {
        var values = segments
            .Select(segment => $"§{segment}")
            .ToArray();

        return values.Length == 0 ? "(none)" : string.Join(", ", values);
    }

    private sealed record EvaluationOptions(
        string DatasetPath,
        VectorSearchMode Mode,
        int RecallK,
        int CandidateK)
    {
        public static EvaluationOptions Parse(IReadOnlyList<string> args)
        {
            string? datasetPath = null;
            var mode = VectorSearchMode.Exact;
            var recallK = 5;
            var candidateK = DeDecretisVectorSearchProfile.MaximumTopK;

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--dataset":
                        datasetPath = ReadValue(args, ref index, "--dataset");
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
                    case "--recall-k":
                        recallK = ParsePositiveInt(
                            ReadValue(args, ref index, "--recall-k"),
                            "--recall-k");
                        break;
                    case "--candidate-k":
                        candidateK = ParsePositiveInt(
                            ReadValue(args, ref index, "--candidate-k"),
                            "--candidate-k");
                        break;
                    default:
                        throw new KnowledgeImportException(
                            $"Unknown evaluate-retrieval option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                throw new KnowledgeImportException(
                    "Missing required option --dataset for evaluate-retrieval.");
            }

            if (recallK > candidateK)
            {
                throw new KnowledgeImportException(
                    "--recall-k cannot be greater than --candidate-k.");
            }

            if (candidateK > DeDecretisVectorSearchProfile.MaximumTopK)
            {
                throw new KnowledgeImportException(
                    $"--candidate-k cannot exceed {DeDecretisVectorSearchProfile.MaximumTopK}.");
            }

            return new EvaluationOptions(
                Path.GetFullPath(datasetPath),
                mode,
                recallK,
                candidateK);
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

    private sealed record RetrievalCaseEvaluation(
        RetrievalEvaluationCase TestCase,
        IReadOnlyList<int> RankedSegments,
        IReadOnlyList<int> TopSegments,
        double RecallAtK,
        double ReciprocalRank,
        int FirstRelevantRank);
}

internal sealed record RetrievalEvaluationDataset(
    string Profile,
    string SourceProfile,
    string SearchProfile,
    RetrievalEvaluationCase[] Cases);

internal sealed record RetrievalEvaluationCase(
    string Id,
    string Language,
    string Query,
    int[] RelevantSegments);
