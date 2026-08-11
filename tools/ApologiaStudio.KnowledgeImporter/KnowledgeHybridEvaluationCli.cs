using System.Globalization;
using System.Text.Json;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeHybridEvaluationCli
{
    private const string EvaluationProfileId =
        "de-decretis-hybrid-retrieval-evaluation-v1";
    private const string FrozenDatasetProfile =
        "de-decretis-retrieval-evaluation-v1";
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
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for evaluate-hybrid.");
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
        if (!string.Equals(modelDigest, digestAfterEmbedding, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama embedding model changed while the hybrid evaluation queries were being embedded.");
        }

        var evaluations = new List<HybridCaseEvaluation>(dataset.Cases.Length);
        var hnswIndexVerified = true;

        for (var index = 0; index < dataset.Cases.Length; index++)
        {
            var testCase = dataset.Cases[index];
            var vector = await KnowledgeVectorSearch.SearchAsync(
                connectionString,
                embeddings[index],
                modelDigest,
                options.CandidateChunkK,
                options.Mode,
                cancellationToken);
            if (options.Mode == VectorSearchMode.Hnsw)
            {
                hnswIndexVerified &= vector.HnswIndexVerified;
            }

            var lexical = await KnowledgeLexicalSearch.SearchAsync(
                connectionString,
                testCase.Query,
                options.CandidateChunkK,
                cancellationToken);
            var hybrid = KnowledgeHybridSearch.Fuse(
                vector,
                lexical,
                DeDecretisHybridSearchProfile.MaximumFusedSegmentK);

            evaluations.Add(EvaluateCase(
                testCase,
                hybrid.Results,
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
                $"Hybrid retrieval evaluation dataset was not found: {path}");
        }

        await using var stream = File.OpenRead(path);
        var dataset = await JsonSerializer.DeserializeAsync<RetrievalEvaluationDataset>(
            stream,
            JsonOptions,
            cancellationToken);

        return dataset ?? throw new KnowledgeImportException(
            "Hybrid retrieval evaluation dataset is empty or invalid JSON.");
    }

    private static void ValidateDataset(RetrievalEvaluationDataset dataset)
    {
        if (!string.Equals(
                dataset.Profile,
                FrozenDatasetProfile,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Hybrid evaluation must reuse the frozen dataset profile '{FrozenDatasetProfile}'.");
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
                $"Frozen evaluation dataset search profile must be '{DeDecretisVectorSearchProfile.ProfileId}'.");
        }

        if (dataset.Cases is not { Length: >= 1 })
        {
            throw new KnowledgeImportException(
                "Hybrid retrieval evaluation dataset must contain at least one case.");
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in dataset.Cases)
        {
            if (string.IsNullOrWhiteSpace(testCase.Id) ||
                !identifiers.Add(testCase.Id))
            {
                throw new KnowledgeImportException(
                    "Every hybrid retrieval evaluation case must have a non-empty unique id.");
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

            if (testCase.RelevantSegments is not { Length: >= 1 } ||
                testCase.RelevantSegments.Any(segment => segment is < 1 or > 32) ||
                testCase.RelevantSegments.Distinct().Count() != testCase.RelevantSegments.Length)
            {
                throw new KnowledgeImportException(
                    $"Evaluation case '{testCase.Id}' contains invalid relevant segments.");
            }
        }
    }

    private static HybridCaseEvaluation EvaluateCase(
        RetrievalEvaluationCase testCase,
        IReadOnlyList<KnowledgeHybridSearchResult> results,
        int recallK)
    {
        var rankedSegments = results
            .Select(result => result.SegmentOrdinal)
            .ToArray();
        var relevant = testCase.RelevantSegments.ToHashSet();
        var topSegments = rankedSegments.Take(recallK).ToArray();
        var relevantInTopK = topSegments.Count(relevant.Contains);
        var recall = (double)relevantInTopK / relevant.Count;

        var firstRelevantRank = 0;
        for (var index = 0; index < rankedSegments.Length; index++)
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

        return new HybridCaseEvaluation(
            testCase,
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
        IReadOnlyList<HybridCaseEvaluation> evaluations)
    {
        Console.WriteLine($"Evaluation profile: {EvaluationProfileId}");
        Console.WriteLine($"Dataset: {options.DatasetPath}");
        Console.WriteLine($"Frozen dataset profile: {dataset.Profile}");
        Console.WriteLine($"Source profile: {dataset.SourceProfile}");
        Console.WriteLine($"Hybrid search profile: {DeDecretisHybridSearchProfile.ProfileId}");
        Console.WriteLine($"Vector profile: {DeDecretisVectorSearchProfile.ProfileId}");
        Console.WriteLine($"Lexical profile: {DeDecretisLexicalSearchProfile.ProfileId}");
        Console.WriteLine($"Vector mode: {options.Mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Fusion strategy: {DeDecretisHybridSearchProfile.FusionStrategy}");
        Console.WriteLine($"RRF constant: {DeDecretisHybridSearchProfile.ReciprocalRankConstant}");
        Console.WriteLine($"Recall K: {options.RecallK}");
        Console.WriteLine($"Candidate chunk K per branch: {options.CandidateChunkK}");
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
        WriteMetrics("METRIC", evaluations, options.RecallK);

        foreach (var language in evaluations
                     .Select(evaluation => evaluation.TestCase.Language)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(language => language, StringComparer.Ordinal))
        {
            var languageEvaluations = evaluations
                .Where(evaluation => string.Equals(
                    evaluation.TestCase.Language,
                    language,
                    StringComparison.Ordinal))
                .ToArray();
            WriteMetrics($"LANGUAGE {language}", languageEvaluations, options.RecallK);
        }
    }

    private static void WriteMetrics(
        string prefix,
        IReadOnlyList<HybridCaseEvaluation> evaluations,
        int recallK)
    {
        var recall = evaluations.Average(evaluation => evaluation.RecallAtK);
        var mrr = evaluations.Average(evaluation => evaluation.ReciprocalRank);
        var hitRate = evaluations.Count(evaluation =>
                evaluation.FirstRelevantRank is > 0 &&
                evaluation.FirstRelevantRank <= recallK) /
            (double)evaluations.Count;

        Console.WriteLine(
            $"{prefix} Recall@{recallK}={recall.ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"{prefix} MRR={mrr.ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"{prefix} HitRate@{recallK}={hitRate.ToString("F6", CultureInfo.InvariantCulture)}");
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
        int CandidateChunkK)
    {
        public static EvaluationOptions Parse(IReadOnlyList<string> args)
        {
            string? datasetPath = null;
            var mode = VectorSearchMode.Exact;
            var recallK = DeDecretisHybridSearchProfile.DefaultTopK;
            var candidateChunkK = DeDecretisHybridSearchProfile.DefaultCandidateChunkK;

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
                        candidateChunkK = ParsePositiveInt(
                            ReadValue(args, ref index, "--candidate-k"),
                            "--candidate-k");
                        break;
                    default:
                        throw new KnowledgeImportException(
                            $"Unknown evaluate-hybrid option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                throw new KnowledgeImportException(
                    "Missing required option --dataset for evaluate-hybrid.");
            }

            if (recallK > DeDecretisHybridSearchProfile.MaximumFusedSegmentK)
            {
                throw new KnowledgeImportException(
                    $"--recall-k cannot exceed {DeDecretisHybridSearchProfile.MaximumFusedSegmentK}.");
            }

            if (candidateChunkK > DeDecretisHybridSearchProfile.MaximumCandidateChunkK)
            {
                throw new KnowledgeImportException(
                    $"--candidate-k cannot exceed {DeDecretisHybridSearchProfile.MaximumCandidateChunkK}.");
            }

            return new EvaluationOptions(
                Path.GetFullPath(datasetPath),
                mode,
                recallK,
                candidateChunkK);
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

    private sealed record HybridCaseEvaluation(
        RetrievalEvaluationCase TestCase,
        IReadOnlyList<int> TopSegments,
        double RecallAtK,
        double ReciprocalRank,
        int FirstRelevantRank);
}
