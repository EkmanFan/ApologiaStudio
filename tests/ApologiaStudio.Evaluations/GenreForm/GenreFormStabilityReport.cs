using System.Text;

namespace ApologiaStudio.Evaluations.GenreForm;

/// <summary>
/// Distinguishes a systematic semantic failure from stochastic instability by
/// repeating identical inferences and reporting the distribution.
/// </summary>
internal sealed record GenreFormStabilityReport(
    string Model,
    string PromptVersion,
    string PolicyVersion,
    int Repetitions,
    DateTimeOffset RunAtUtc,
    IReadOnlyList<GenreFormStabilityCase> Cases)
{
    public string ToMarkdown(Func<string, string> label)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Genre/Form MRA stability study — EVAL-2");
        builder.AppendLine();
        builder.AppendLine($"- Run: {RunAtUtc:u}");
        builder.AppendLine($"- Model: `{Model}`");
        builder.AppendLine($"- Prompt: `{PromptVersion}`");
        builder.AppendLine($"- Policy: `{PolicyVersion}`");
        builder.AppendLine($"- Repetitions per case: {Repetitions}");
        builder.AppendLine(
            "- Nothing was changed from the accepted baseline: same model, " +
            "prompt, evidence, policy and inference options.");
        builder.AppendLine();

        builder.AppendLine("## Per case");
        builder.AppendLine();
        builder.AppendLine(
            "| Case | Expected | Exact | Insufficient | Failures | Payload | " +
            "Latency ms (min/med/max) |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---|");

        foreach (var item in Cases)
        {
            var latencies = item.Results
                .Select(x => x.LatencyMilliseconds)
                .Order()
                .ToList();

            builder.AppendLine(
                $"| {item.Case.Id} " +
                $"| {Describe(item.Expected, label)} " +
                $"| {item.ExactCount}/{item.Results.Count} " +
                $"| {item.InsufficientCount} " +
                $"| {item.ContractFailures + item.InferenceFailures} " +
                $"| {item.Case.PayloadCharacters} " +
                $"| {latencies[0]:F0} / {latencies[latencies.Count / 2]:F0} / {latencies[^1]:F0} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Suggested-term frequency");
        builder.AppendLine();

        foreach (var item in Cases)
        {
            builder.AppendLine($"### {item.Case.Id}");
            builder.AppendLine();

            if (item.TermFrequency.Count == 0)
            {
                builder.AppendLine("No term was ever proposed.");
                builder.AppendLine();
                continue;
            }

            foreach (var (uri, count) in item.TermFrequency.OrderByDescending(x => x.Value))
            {
                var expected = item.Expected.Contains(uri) ? "expected" : "unexpected";
                builder.AppendLine(
                    $"- {label(uri)}: {count}/{item.Results.Count} ({expected})");
            }

            builder.AppendLine();
        }

        var tokens = Cases
            .SelectMany(x => x.Results)
            .Where(x => x.OutputTokenCount is not null)
            .Select(x => x.OutputTokenCount!.Value)
            .Order()
            .ToList();

        if (tokens.Count > 0)
        {
            builder.AppendLine("## Tokens");
            builder.AppendLine();
            builder.AppendLine(
                $"- Output tokens: min {tokens[0]}, median {tokens[tokens.Count / 2]}, " +
                $"max {tokens[^1]}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string Describe(
        IReadOnlySet<string> terms,
        Func<string, string> label) =>
        terms.Count == 0
            ? "∅"
            : string.Join(", ", terms.Select(label).Order(StringComparer.Ordinal));
}

internal sealed record GenreFormStabilityCase(
    GenreFormEvaluationCase Case,
    IReadOnlySet<string> Expected,
    IReadOnlyList<GenreFormCaseResult> Results)
{
    public int ExactCount => Results.Count(x => x.ExactMatch);

    public int InsufficientCount => Results.Count(x => x.InsufficientEvidence);

    public int ContractFailures =>
        Results.Count(x => x.Status == GenreFormCaseStatus.ContractFailure);

    public int InferenceFailures =>
        Results.Count(x => x.Status == GenreFormCaseStatus.InferenceFailure);

    public IReadOnlyDictionary<string, int> TermFrequency =>
        Results
            .SelectMany(x => x.Suggested)
            .GroupBy(x => x, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
}
