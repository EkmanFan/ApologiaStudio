using System.Globalization;
using System.Text.Json;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeLexicalEvaluationCli
{
    private const string EvaluationProfileId =
        "de-decretis-lexical-retrieval-evaluation-v1";
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
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for evaluate-lexical.");
        }

        var evaluations = new List<LexicalCaseEvaluation>(dataset.Cases.Length);
        foreach (var testCase in dataset.Cases)
        {
            var response = await KnowledgeLexicalSearch.SearchAsync(
                connectionString,
                testCase.Query,
                options.CandidateK,
                cancellationToken);

            evaluations.Add(EvaluateCase(
                testCase,
                response.Results,
                options.RecallK));
        }

        WriteResult(options, dataset, evaluations);
        return 0;
    }

    private static async Task<RetrievalEvaluationDataset> LoadDatasetAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new KnowledgeImportException(
                $"Lexical retrieval evaluation dataset was not found: {path}");
        }

        await using var stream = File.OpenRead(path);
        var dataset = await JsonSerializer.DeserializeAsync<RetrievalEvaluationDataset>(
            stream,
            JsonOptions,
            cancellationToken);

        return dataset ?? throw new KnowledgeImportException(
            "Lexical retrieval evaluation dataset is empty or invalid JSON.");
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
                DeDecretisLexicalSearchProfile.ProfileId,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Evaluation dataset search profile must be '{DeDecretisLexicalSearchProfile.ProfileId}'.");
        }

        if (dataset.Cases is not { Length: >= 1 })
        {
            throw new KnowledgeImportException(
                "Lexical retrieval evaluation dataset must contain at least one case.");
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in dataset.Cases)
        {
            if (string.IsNullOrWhiteSpace(testCase.Id) ||
                !identifiers.Add(testCase.Id))
            {
                throw new KnowledgeImportException(
                    "Every lexical retrieval evaluation case must have a non-empty unique id.");
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

    private static LexicalCaseEvaluation EvaluateCase(
        RetrievalEvaluationCase testCase,
        IReadOnlyList<KnowledgeLexicalSearchResult> chunkResults,
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

        return new LexicalCaseEvaluation(
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
        IReadOnlyList<LexicalCaseEvaluation> evaluations)
    {
        Console.WriteLine($"Evaluation profile: {EvaluationProfileId}");
        Console.WriteLine($"Dataset: {options.DatasetPath}");
        Console.WriteLine($"Source profile: {dataset.SourceProfile}");
        Console.WriteLine($"Search profile: {dataset.SearchProfile}");
        Console.WriteLine(
            $"Text search configuration: {DeDecretisLexicalSearchProfile.TextSearchConfiguration}");
        Console.WriteLine($"Query strategy: {DeDecretisLexicalSearchProfile.QueryStrategy}");
        Console.WriteLine($"Recall K: {options.RecallK}");
        Console.WriteLine($"Candidate chunk K: {options.CandidateK}");
        Console.WriteLine($"Cases: {evaluations.Count}");
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
        IReadOnlyList<LexicalCaseEvaluation> evaluations,
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
        int RecallK,
        int CandidateK)
    {
        public static EvaluationOptions Parse(IReadOnlyList<string> args)
        {
            string? datasetPath = null;
            var recallK = 5;
            var candidateK = DeDecretisLexicalSearchProfile.MaximumTopK;

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--dataset":
                        datasetPath = ReadValue(args, ref index, "--dataset");
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
                            $"Unknown evaluate-lexical option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                throw new KnowledgeImportException(
                    "Missing required option --dataset for evaluate-lexical.");
            }

            if (recallK > candidateK)
            {
                throw new KnowledgeImportException(
                    "--recall-k cannot be greater than --candidate-k.");
            }

            if (candidateK > DeDecretisLexicalSearchProfile.MaximumTopK)
            {
                throw new KnowledgeImportException(
                    $"--candidate-k cannot exceed {DeDecretisLexicalSearchProfile.MaximumTopK}.");
            }

            return new EvaluationOptions(
                Path.GetFullPath(datasetPath),
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

    private sealed record LexicalCaseEvaluation(
        RetrievalEvaluationCase TestCase,
        IReadOnlyList<int> RankedSegments,
        IReadOnlyList<int> TopSegments,
        double RecallAtK,
        double ReciprocalRank,
        int FirstRelevantRank);
}
