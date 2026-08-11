using System.Globalization;
using System.Text.Json;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeRerankerEvaluationCli
{
    private const string FrozenDatasetProfile = "de-decretis-retrieval-evaluation-v1";
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var options = EvaluationOptions.Parse(args);
        var dataset = await LoadDatasetAsync(options.DatasetPath, cancellationToken);
        ValidateDataset(dataset);
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new KnowledgeImportException(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for evaluate-reranker.");
        }

        using var embeddingClient = new OllamaEmbeddingClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress));
        var embeddingDigest = await embeddingClient.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        var embeddingInputs = dataset.Cases
            .Select(testCase => DeDecretisVectorSearchProfile.FormatQuery(testCase.Query))
            .ToArray();
        var embeddings = await embeddingClient.EmbedAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            DeDecretisRetrievalProfile.EmbeddingDimensions,
            embeddingInputs,
            cancellationToken);
        var embeddingDigestAfter = await embeddingClient.ResolveModelDigestAsync(
            DeDecretisRetrievalProfile.EmbeddingModel,
            cancellationToken);
        if (!string.Equals(embeddingDigest, embeddingDigestAfter, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama embedding model changed while reranker evaluation queries were being embedded.");
        }

        using var rerankerClient = new OllamaListwiseRerankerClient(
            new Uri(DeDecretisRetrievalProfile.OllamaBaseAddress),
            TimeSpan.FromSeconds(DeDecretisRerankerProfile.TimeoutSeconds));
        var rerankerDigest = await rerankerClient.ResolveModelDigestAsync(
            DeDecretisRerankerProfile.RerankerModel,
            cancellationToken);

        var evaluations = new List<RerankerCaseEvaluation>(dataset.Cases.Length);
        var hnswIndexVerified = true;
        long totalRerankNanoseconds = 0;
        long totalLoadNanoseconds = 0;
        var rerankDurationsReported = 0;
        for (var index = 0; index < dataset.Cases.Length; index++)
        {
            var testCase = dataset.Cases[index];
            var vector = await KnowledgeVectorSearch.SearchAsync(
                connectionString,
                embeddings[index],
                embeddingDigest,
                DeDecretisRerankerProfile.CandidateChunkK,
                options.Mode,
                cancellationToken);
            if (options.Mode == VectorSearchMode.Hnsw)
            {
                hnswIndexVerified &= vector.HnswIndexVerified;
            }

            var candidates = KnowledgeReranker.BuildCandidates(
                vector,
                options.CandidateSegmentK);
            var rerankResult = await rerankerClient.RerankAsync(
                DeDecretisRerankerProfile.RerankerModel,
                testCase.Query,
                candidates,
                cancellationToken);
            if (rerankResult.TotalDurationNanoseconds is { } totalDuration)
            {
                totalRerankNanoseconds += totalDuration;
                rerankDurationsReported++;
            }
            if (rerankResult.LoadDurationNanoseconds is { } loadDuration)
            {
                totalLoadNanoseconds += loadDuration;
            }

            var reranked = KnowledgeReranker.ApplyOrdering(
                candidates,
                rerankResult.OrderedIds);
            evaluations.Add(EvaluateCase(
                testCase,
                candidates,
                reranked,
                options.RecallK));
        }

        var rerankerDigestAfter = await rerankerClient.ResolveModelDigestAsync(
            DeDecretisRerankerProfile.RerankerModel,
            cancellationToken);
        if (!string.Equals(rerankerDigest, rerankerDigestAfter, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The Ollama reranker model changed during the reranker evaluation.");
        }

        WriteResult(
            options,
            dataset,
            embeddingDigest,
            rerankerDigest,
            hnswIndexVerified,
            totalRerankNanoseconds,
            totalLoadNanoseconds,
            rerankDurationsReported,
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
                $"Reranker evaluation dataset was not found: {path}");
        }

        await using var stream = File.OpenRead(path);
        var dataset = await JsonSerializer.DeserializeAsync<RetrievalEvaluationDataset>(
            stream,
            JsonOptions,
            cancellationToken);
        return dataset ?? throw new KnowledgeImportException(
            "Reranker evaluation dataset is empty or invalid JSON.");
    }

    private static void ValidateDataset(RetrievalEvaluationDataset dataset)
    {
        if (!string.Equals(dataset.Profile, FrozenDatasetProfile, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Reranker evaluation must reuse the frozen dataset profile '{FrozenDatasetProfile}'.");
        }
        if (!string.Equals(dataset.SourceProfile, DeDecretisDocument.ProfileId, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Evaluation dataset source profile must be '{DeDecretisDocument.ProfileId}'.");
        }
        if (!string.Equals(dataset.SearchProfile, DeDecretisVectorSearchProfile.ProfileId, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Frozen evaluation dataset search profile must be '{DeDecretisVectorSearchProfile.ProfileId}'.");
        }
        if (dataset.Cases is not { Length: >= 1 })
        {
            throw new KnowledgeImportException(
                "Reranker evaluation dataset must contain at least one case.");
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in dataset.Cases)
        {
            if (string.IsNullOrWhiteSpace(testCase.Id) || !identifiers.Add(testCase.Id))
            {
                throw new KnowledgeImportException(
                    "Every reranker evaluation case must have a non-empty unique id.");
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

    private static RerankerCaseEvaluation EvaluateCase(
        RetrievalEvaluationCase testCase,
        IReadOnlyList<RerankerCandidate> candidates,
        IReadOnlyList<RerankedSegment> reranked,
        int recallK)
    {
        var relevant = testCase.RelevantSegments.ToHashSet();
        var candidateSegments = candidates
            .Select(candidate => candidate.Evidence.SegmentOrdinal)
            .ToArray();
        var candidateRelevantCount = candidateSegments.Count(relevant.Contains);
        var candidateRecall = (double)candidateRelevantCount / relevant.Count;

        var rankedSegments = reranked
            .Select(result => result.Evidence.SegmentOrdinal)
            .ToArray();
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

        var reciprocalRank = firstRelevantRank == 0 ? 0d : 1d / firstRelevantRank;
        return new RerankerCaseEvaluation(
            testCase,
            candidateSegments,
            topSegments,
            candidateRecall,
            recall,
            reciprocalRank,
            firstRelevantRank);
    }

    private static void WriteResult(
        EvaluationOptions options,
        RetrievalEvaluationDataset dataset,
        string embeddingDigest,
        string rerankerDigest,
        bool hnswIndexVerified,
        long totalRerankNanoseconds,
        long totalLoadNanoseconds,
        int rerankDurationsReported,
        IReadOnlyList<RerankerCaseEvaluation> evaluations)
    {
        Console.WriteLine($"Evaluation profile: {DeDecretisRerankerProfile.EvaluationProfileId}");
        Console.WriteLine($"Dataset: {options.DatasetPath}");
        Console.WriteLine($"Frozen dataset profile: {dataset.Profile}");
        Console.WriteLine($"Source profile: {dataset.SourceProfile}");
        Console.WriteLine($"Vector profile: {DeDecretisVectorSearchProfile.ProfileId}");
        Console.WriteLine($"Reranker profile: {DeDecretisRerankerProfile.ProfileId}");
        Console.WriteLine($"Vector mode: {options.Mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Recall K: {options.RecallK}");
        Console.WriteLine($"Candidate chunk K: {DeDecretisRerankerProfile.CandidateChunkK}");
        Console.WriteLine($"Candidate segment K: {options.CandidateSegmentK}");
        Console.WriteLine($"Cases: {evaluations.Count}");
        Console.WriteLine($"Embedding model: {DeDecretisRetrievalProfile.EmbeddingModel}");
        Console.WriteLine($"Embedding digest: {embeddingDigest}");
        Console.WriteLine($"Reranker kind: {DeDecretisRerankerProfile.RerankerKind}");
        Console.WriteLine($"Reranker model: {DeDecretisRerankerProfile.RerankerModel}");
        Console.WriteLine($"Reranker digest: {rerankerDigest}");
        Console.WriteLine(
            $"HNSW index verified: {(options.Mode == VectorSearchMode.Hnsw ? (hnswIndexVerified ? "yes" : "no") : "not requested")}");
        Console.WriteLine("RESULT: EVALUATED");

        foreach (var evaluation in evaluations)
        {
            Console.WriteLine();
            Console.WriteLine($"CASE: {evaluation.TestCase.Id}");
            Console.WriteLine($"Language: {evaluation.TestCase.Language}");
            Console.WriteLine($"Query: {evaluation.TestCase.Query}");
            Console.WriteLine($"Relevant: {FormatSegments(evaluation.TestCase.RelevantSegments)}");
            Console.WriteLine($"Vector candidates: {FormatSegments(evaluation.CandidateSegments)}");
            Console.WriteLine($"Reranked@{options.RecallK}: {FormatSegments(evaluation.TopSegments)}");
            Console.WriteLine(
                $"CandidateRecall@{options.CandidateSegmentK}: {evaluation.CandidateRecall.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine(
                $"Recall@{options.RecallK}: {evaluation.RecallAtK.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine(
                $"Reciprocal rank: {evaluation.ReciprocalRank.ToString("F6", CultureInfo.InvariantCulture)}");
            Console.WriteLine(
                $"First relevant rank: {(evaluation.FirstRelevantRank == 0 ? "none" : evaluation.FirstRelevantRank.ToString(CultureInfo.InvariantCulture))}");
        }

        Console.WriteLine();
        Console.WriteLine($"CASES: {evaluations.Count}");
        WriteMetrics("METRIC", evaluations, options.RecallK, options.CandidateSegmentK);
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
            WriteMetrics(
                $"LANGUAGE {language}",
                languageEvaluations,
                options.RecallK,
                options.CandidateSegmentK);
        }

        var totalMs = totalRerankNanoseconds / 1_000_000d;
        var loadMs = totalLoadNanoseconds / 1_000_000d;
        var averageMs = rerankDurationsReported == 0 ? 0d : totalMs / rerankDurationsReported;
        Console.WriteLine(
            $"RERANKER TotalMs={totalMs.ToString("F1", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"RERANKER AverageMs={averageMs.ToString("F1", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"RERANKER LoadMs={loadMs.ToString("F1", CultureInfo.InvariantCulture)}");
    }

    private static void WriteMetrics(
        string prefix,
        IReadOnlyList<RerankerCaseEvaluation> evaluations,
        int recallK,
        int candidateSegmentK)
    {
        var candidateRecall = evaluations.Average(evaluation => evaluation.CandidateRecall);
        var recall = evaluations.Average(evaluation => evaluation.RecallAtK);
        var mrr = evaluations.Average(evaluation => evaluation.ReciprocalRank);
        var hitRate = evaluations.Count(evaluation =>
                evaluation.FirstRelevantRank is > 0 &&
                evaluation.FirstRelevantRank <= recallK) /
            (double)evaluations.Count;
        Console.WriteLine(
            $"{prefix} CandidateRecall@{candidateSegmentK}={candidateRecall.ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"{prefix} Recall@{recallK}={recall.ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"{prefix} MRR={mrr.ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine(
            $"{prefix} HitRate@{recallK}={hitRate.ToString("F6", CultureInfo.InvariantCulture)}");
    }

    private static string FormatSegments(IEnumerable<int> segments)
    {
        var values = segments.Select(segment => $"§{segment}").ToArray();
        return values.Length == 0 ? "(none)" : string.Join(", ", values);
    }

    private sealed record EvaluationOptions(
        string DatasetPath,
        VectorSearchMode Mode,
        int RecallK,
        int CandidateSegmentK)
    {
        public static EvaluationOptions Parse(IReadOnlyList<string> args)
        {
            string? datasetPath = null;
            var mode = VectorSearchMode.Exact;
            var recallK = DeDecretisRerankerProfile.DefaultTopK;
            var candidateSegmentK = DeDecretisRerankerProfile.CandidateSegmentK;
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
                    case "--candidate-segment-k":
                        candidateSegmentK = ParsePositiveInt(
                            ReadValue(args, ref index, "--candidate-segment-k"),
                            "--candidate-segment-k");
                        break;
                    default:
                        throw new KnowledgeImportException(
                            $"Unknown evaluate-reranker option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                throw new KnowledgeImportException(
                    "Missing required option --dataset for evaluate-reranker.");
            }
            if (candidateSegmentK > DeDecretisRerankerProfile.MaximumTopK)
            {
                throw new KnowledgeImportException(
                    $"--candidate-segment-k cannot exceed {DeDecretisRerankerProfile.MaximumTopK}.");
            }
            if (recallK > candidateSegmentK)
            {
                throw new KnowledgeImportException(
                    "--recall-k cannot be greater than --candidate-segment-k.");
            }

            return new EvaluationOptions(
                Path.GetFullPath(datasetPath),
                mode,
                recallK,
                candidateSegmentK);
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

    private sealed record RerankerCaseEvaluation(
        RetrievalEvaluationCase TestCase,
        IReadOnlyList<int> CandidateSegments,
        IReadOnlyList<int> TopSegments,
        double CandidateRecall,
        double RecallAtK,
        double ReciprocalRank,
        int FirstRelevantRank);
}
