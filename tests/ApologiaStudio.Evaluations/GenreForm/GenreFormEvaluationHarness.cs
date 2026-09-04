using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Evaluations.Support;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;

namespace ApologiaStudio.Evaluations.GenreForm;

/// <summary>
/// Reproducible baseline for Genre/Form suggestion quality.
///
/// It keeps two questions apart. Deterministic policy validation answers
/// "did the model respect the contract?"; only responses that pass it are
/// scored for semantic quality. A rejected response is a contract failure and
/// is excluded from accuracy, never counted as a wrong classification.
/// </summary>
internal sealed class GenreFormEvaluationHarness
{
    private readonly GenreFormPolicySnapshot _policy;

    private readonly IGenreFormClassifier _classifier;

    private readonly RecordingStructuredGenerationTelemetry _telemetry;

    private GenreFormEvaluationHarness(
        GenreFormPolicySnapshot policy,
        IGenreFormClassifier classifier,
        RecordingStructuredGenerationTelemetry telemetry)
    {
        _policy = policy;
        _classifier = classifier;
        _telemetry = telemetry;
    }

    public string Model { get; private init; } = string.Empty;

    public static GenreFormEvaluationHarness Create(string model)
    {
        var policy = LoadPolicy();
        var telemetry = new RecordingStructuredGenerationTelemetry();

        var settings = new AiRuntimeSettingsSnapshot(
            AiRuntimeSettingsSnapshot.OllamaProvider,
            LocalModelEvaluationSupport.GetBaseAddress().ToString(),
            model,
            model,
            30,
            180,
            "5m",
            24,
            24_000,
            800,
            DateTimeOffset.UtcNow,
            new Dictionary<Guid, string>());

        var runtime = new OllamaStructuredGenerationRuntime(
            new EvaluationAiRuntimeSettingsStore(settings),
            new EvaluationOllamaHttpClientFactory(),
            telemetry);

        var classifier = new StructuredGenreFormClassifier(
            runtime,
            new StaticGenreFormPolicyProvider(policy),
            new GenreFormClassificationValidator(),
            TimeProvider.System);

        return new GenreFormEvaluationHarness(policy, classifier, telemetry)
        {
            Model = model
        };
    }

    /// <summary>
    /// The active profile is rebuilt from the official LCGFT subset so the
    /// baseline runs without a database. It must match the product profile.
    /// </summary>
    private static GenreFormPolicySnapshot LoadPolicy()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "GenreForm",
            "lcgft-profile-v1-fixture.jsonl");

        using var content = File.OpenRead(path);
        var dataset = new SkosJsonLdGenreFormDatasetReader().Read(content);

        var byUri = dataset.Terms.ToDictionary(x => x.AuthorityUri, StringComparer.Ordinal);

        var selectable = GenreFormProfile.SelectableLabels
            .Select(label => dataset.Terms.Single(x => x.PreferredLabel == label))
            .ToList();

        var terms = new List<GenreFormPolicyTerm>();

        foreach (var term in dataset.Terms)
        {
            var isSelectable = selectable.Any(
                x => string.Equals(x.AuthorityUri, term.AuthorityUri, StringComparison.Ordinal));

            terms.Add(new GenreFormPolicyTerm(
                term.AuthorityUri,
                term.AuthorityIdentifier,
                term.PreferredLabel,
                isSelectable
                    ? GenreFormPolicyUsage.Selectable
                    : GenreFormPolicyUsage.StructuralOnly,
                Ancestors(term.AuthorityUri, byUri)));
        }

        return new GenreFormPolicySnapshot(GenreFormProfile.Version, terms);
    }

    private static IReadOnlyList<string> Ancestors(
        string authorityUri,
        IReadOnlyDictionary<string, GenreFormAuthorityTerm> byUri)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new List<string> { authorityUri };

        while (frontier.Count > 0)
        {
            var next = new List<string>();

            foreach (var current in frontier)
            {
                if (!byUri.TryGetValue(current, out var term))
                {
                    continue;
                }

                next.AddRange(term.BroaderAuthorityUris.Where(seen.Add));
            }

            frontier = next;
        }

        return seen.ToList();
    }

    public async Task<GenreFormEvaluationReport> RunAsync(
        IReadOnlyList<GenreFormEvaluationCase> cases,
        CancellationToken cancellationToken)
    {
        var results = new List<GenreFormCaseResult>();

        foreach (var evaluationCase in cases)
        {
            results.Add(await RunCaseAsync(evaluationCase, cancellationToken));
        }

        return new GenreFormEvaluationReport(
            Model,
            StructuredGenreFormClassifier.PromptVersion,
            _policy.PolicyVersion,
            _policy.SelectableTerms.Count(),
            DateTimeOffset.UtcNow,
            results);
    }

    /// <summary>
    /// Repeats one case unchanged. Inference parameters are untouched, so any
    /// variation observed is the model's own sampling, not configuration.
    /// </summary>
    public async Task<IReadOnlyList<GenreFormCaseResult>> RepeatAsync(
        GenreFormEvaluationCase evaluationCase,
        int repetitions,
        CancellationToken cancellationToken)
    {
        var results = new List<GenreFormCaseResult>();

        for (var index = 0; index < repetitions; index++)
        {
            results.Add(await RunCaseAsync(evaluationCase, cancellationToken));
        }

        return results;
    }

    private async Task<GenreFormCaseResult> RunCaseAsync(
        GenreFormEvaluationCase evaluationCase,
        CancellationToken cancellationToken)
    {
        var expected = evaluationCase.Expected
            .Select(Resolve)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToHashSet(StringComparer.Ordinal);

        var startedAt = Stopwatch.GetTimestamp();
        var telemetryBefore = _telemetry.Completed.Count;

        try
        {
            var validation = await _classifier.ClassifyAsync(
                evaluationCase.ToEvidence(),
                cancellationToken);

            var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var observation = _telemetry.Completed.Count > telemetryBefore
                ? _telemetry.Completed[^1]
                : null;

            if (!validation.IsValid)
            {
                // Contract failure: the response never becomes a classification.
                return GenreFormCaseResult.ContractFailure(
                    evaluationCase,
                    expected,
                    string.Join(
                        " ",
                        validation.Errors.Select(x => x.Failure.ToString())),
                    elapsed,
                    observation);
            }

            var suggested = validation.Result!.Suggested
                .Select(x => x.AuthorityUri)
                .ToHashSet(StringComparer.Ordinal);

            return GenreFormCaseResult.Classified(
                evaluationCase,
                expected,
                suggested,
                validation.Result.InsufficientEvidence,
                elapsed,
                observation);
        }
        catch (Exception exception)
        {
            return GenreFormCaseResult.InferenceFailure(
                evaluationCase,
                expected,
                exception.GetType().Name,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    private string? Resolve(string authorityId) =>
        GenreFormSelectionRules.Resolve(authorityId, _policy)?.AuthorityUri;

    public string LabelFor(string authorityUri) =>
        _policy.Find(authorityUri)?.PreferredLabel ?? authorityUri;

    private sealed class StaticGenreFormPolicyProvider(
        GenreFormPolicySnapshot policy)
        : IGenreFormPolicyProvider
    {
        public Task<GenreFormPolicySnapshot> GetActivePolicyAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(policy);
    }

    private sealed class RecordingStructuredGenerationTelemetry
        : IStructuredGenerationTelemetry
    {
        public List<StructuredGenerationCompletedObservation> Completed { get; } = [];

        public void GenerationStarted(
            StructuredGenerationStartedObservation observation)
        {
        }

        public void GenerationCompleted(
            StructuredGenerationCompletedObservation observation) =>
            Completed.Add(observation);

        public void GenerationFailed(
            StructuredGenerationFailedObservation observation)
        {
        }
    }
}

internal sealed class GenreFormEvaluationCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "reference";

    [JsonPropertyName("source")]
    public string Source { get; init; } = "curated";

    [JsonPropertyName("note")]
    public string? Note { get; init; }

    [JsonPropertyName("expected")]
    public IReadOnlyList<string> Expected { get; init; } = [];

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("contributors")]
    public IReadOnlyList<string> Contributors { get; init; } = [];

    [JsonPropertyName("languageCode")]
    public string? LanguageCode { get; init; }

    [JsonPropertyName("editionStatement")]
    public string? EditionStatement { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    public MetadataReviewEvidence ToEvidence() =>
        new(
            Title,
            null,
            Contributors,
            LanguageCode,
            EditionStatement,
            null,
            null,
            Description,
            []);

    public static IReadOnlyList<GenreFormEvaluationCase> Load()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "GenreForm",
            "genre-form-evaluation-cases.json");

        using var stream = File.OpenRead(path);

        return JsonSerializer.Deserialize<List<GenreFormEvaluationCase>>(stream)
               ?? throw new InvalidOperationException(
                   "The evaluation case set could not be read.");
    }
}

internal enum GenreFormCaseStatus
{
    Classified = 0,
    ContractFailure = 1,
    InferenceFailure = 2
}

internal sealed record GenreFormCaseResult(
    GenreFormEvaluationCase Case,
    GenreFormCaseStatus Status,
    IReadOnlySet<string> Expected,
    IReadOnlySet<string> Suggested,
    bool InsufficientEvidence,
    string? FailureDetail,
    double LatencyMilliseconds,
    int? PromptTokenCount,
    int? OutputTokenCount)
{
    public bool ExactMatch =>
        Status == GenreFormCaseStatus.Classified && Expected.SetEquals(Suggested);

    public IEnumerable<string> FalsePositives => Suggested.Except(Expected);

    public IEnumerable<string> FalseNegatives => Expected.Except(Suggested);

    public static GenreFormCaseResult Classified(
        GenreFormEvaluationCase evaluationCase,
        IReadOnlySet<string> expected,
        IReadOnlySet<string> suggested,
        bool insufficientEvidence,
        double latency,
        StructuredGenerationCompletedObservation? observation) =>
        new(
            evaluationCase,
            GenreFormCaseStatus.Classified,
            expected,
            suggested,
            insufficientEvidence,
            null,
            latency,
            observation?.PromptTokenCount,
            observation?.OutputTokenCount);

    public static GenreFormCaseResult ContractFailure(
        GenreFormEvaluationCase evaluationCase,
        IReadOnlySet<string> expected,
        string detail,
        double latency,
        StructuredGenerationCompletedObservation? observation) =>
        new(
            evaluationCase,
            GenreFormCaseStatus.ContractFailure,
            expected,
            new HashSet<string>(StringComparer.Ordinal),
            false,
            detail,
            latency,
            observation?.PromptTokenCount,
            observation?.OutputTokenCount);

    public static GenreFormCaseResult InferenceFailure(
        GenreFormEvaluationCase evaluationCase,
        IReadOnlySet<string> expected,
        string detail,
        double latency) =>
        new(
            evaluationCase,
            GenreFormCaseStatus.InferenceFailure,
            expected,
            new HashSet<string>(StringComparer.Ordinal),
            false,
            detail,
            latency,
            null,
            null);
}

internal sealed record GenreFormEvaluationReport(
    string Model,
    string PromptVersion,
    string PolicyVersion,
    int SelectableTermCount,
    DateTimeOffset RunAtUtc,
    IReadOnlyList<GenreFormCaseResult> Results)
{
    public IEnumerable<GenreFormCaseResult> Scored =>
        Results.Where(x => x.Status == GenreFormCaseStatus.Classified);

    public int ContractFailures =>
        Results.Count(x => x.Status == GenreFormCaseStatus.ContractFailure);

    public int InferenceFailures =>
        Results.Count(x => x.Status == GenreFormCaseStatus.InferenceFailure);

    public string ToMarkdown(Func<string, string> label)
    {
        var builder = new StringBuilder();
        var scored = Scored.ToList();

        builder.AppendLine("# Genre/Form MRA evaluation baseline");
        builder.AppendLine();
        builder.AppendLine($"- Run: {RunAtUtc:u}");
        builder.AppendLine($"- Model: `{Model}`");
        builder.AppendLine($"- Prompt: `{PromptVersion}`");
        builder.AppendLine($"- Policy: `{PolicyVersion}` ({SelectableTermCount} selectable terms)");
        builder.AppendLine(
            "- Evidence: curated metadata-level fixtures. No end-to-end " +
            "real-document run: the editorial workflow supplies metadata only.");
        builder.AppendLine();

        builder.AppendLine("## Contract vs semantics");
        builder.AppendLine();
        builder.AppendLine(
            "Deterministic policy validation is reported separately from model " +
            "quality. A response rejected by the validator is a contract " +
            "failure and is excluded from accuracy.");
        builder.AppendLine();
        builder.AppendLine($"- Cases: {Results.Count}");
        builder.AppendLine($"- Scored for semantics: {scored.Count}");
        builder.AppendLine($"- Contract failures: {ContractFailures}");
        builder.AppendLine($"- Inference failures: {InferenceFailures}");
        builder.AppendLine();

        if (scored.Count > 0)
        {
            var exact = scored.Count(x => x.ExactMatch);
            var emptyExpected = scored.Where(x => x.Expected.Count == 0).ToList();
            var emptyCorrect = emptyExpected.Count(x => x.Suggested.Count == 0);

            builder.AppendLine("## Accuracy");
            builder.AppendLine();
            builder.AppendLine(
                $"- Exact-set correctness: {exact}/{scored.Count} " +
                $"({(double)exact / scored.Count:P0})");
            builder.AppendLine(
                $"- Correct empty classifications: {emptyCorrect}/{emptyExpected.Count}");

            var truePositives = scored.Sum(x => x.Expected.Intersect(x.Suggested).Count());
            var falsePositives = scored.Sum(x => x.FalsePositives.Count());
            var falseNegatives = scored.Sum(x => x.FalseNegatives.Count());

            builder.AppendLine(
                $"- Term-level: TP={truePositives} FP={falsePositives} FN={falseNegatives}");
            builder.AppendLine(
                "- Declared insufficient evidence: " +
                $"{scored.Count(x => x.InsufficientEvidence)}/{scored.Count}");

            if (truePositives + falsePositives > 0)
            {
                builder.AppendLine(
                    "- Precision: " +
                    $"{(double)truePositives / (truePositives + falsePositives):P0}");
            }

            if (truePositives + falseNegatives > 0)
            {
                builder.AppendLine(
                    "- Recall: " +
                    $"{(double)truePositives / (truePositives + falseNegatives):P0}");
            }

            builder.AppendLine();

            builder.AppendLine("## Cost");
            builder.AppendLine();
            var latencies = Results.Select(x => x.LatencyMilliseconds).Order().ToList();
            builder.AppendLine(
                $"- Latency: median {latencies[latencies.Count / 2]:F0} ms, " +
                $"max {latencies[^1]:F0} ms");
            builder.AppendLine(
                $"- Prompt tokens: median " +
                $"{Median(Results.Select(x => x.PromptTokenCount))}");
            builder.AppendLine(
                $"- Output tokens: median " +
                $"{Median(Results.Select(x => x.OutputTokenCount))}");
            builder.AppendLine();
        }

        builder.AppendLine("## Cases");
        builder.AppendLine();
        builder.AppendLine(
            "| Case | Kind | Expected | Suggested | Insufficient | Result | ms |");
        builder.AppendLine("|---|---|---|---|---|---|---:|");

        foreach (var result in Results)
        {
            var verdict = result.Status switch
            {
                GenreFormCaseStatus.ContractFailure =>
                    $"contract failure ({result.FailureDetail})",
                GenreFormCaseStatus.InferenceFailure =>
                    $"inference failure ({result.FailureDetail})",
                _ => result.ExactMatch ? "exact" : "mismatch"
            };

            builder.AppendLine(
                $"| {result.Case.Id} | {result.Case.Kind} " +
                $"| {Describe(result.Expected, label)} " +
                $"| {Describe(result.Suggested, label)} " +
                $"| {(result.InsufficientEvidence ? "yes" : "")} " +
                $"| {verdict} | {result.LatencyMilliseconds:F0} |");
        }

        return builder.ToString();
    }

    private static string Describe(
        IReadOnlySet<string> terms,
        Func<string, string> label) =>
        terms.Count == 0
            ? "∅"
            : string.Join(", ", terms.Select(label).Order(StringComparer.Ordinal));

    private static string Median(IEnumerable<int?> values)
    {
        var ordered = values.Where(x => x is not null).Select(x => x!.Value).Order().ToList();
        return ordered.Count == 0 ? "—" : ordered[ordered.Count / 2].ToString();
    }
}
