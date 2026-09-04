using System.Text;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Evaluations.Support;

namespace ApologiaStudio.Evaluations.GenreForm;

/// <summary>
/// EVAL-4 diagnostics. Nothing here changes production behaviour: the
/// experimental framings live in this project only.
///
/// Methodology note, deliberately explicit. A ten-run suite is regression and
/// smoke evidence: it separates "always" from "never" and nothing finer. A
/// proportion between those extremes needs a targeted larger sample and an
/// interval, and none of these numbers should be read as a production-quality
/// estimate.
/// </summary>
public sealed class GenreFormDiagnosticTests
{
    [Fact]
    public async Task De_decretis_variance_is_characterized()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = ModelName();
        var repetitions = Repetitions("GENRE_FORM_VARIANCE_REPETITIONS", 50);

        var harness = GenreFormEvaluationHarness.Create(model);
        var all = GenreFormEvaluationCase.Load();

        var builder = new StringBuilder();
        builder.AppendLine("# De Decretis variance — EVAL-4");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{model}`");
        builder.AppendLine($"- Repetitions per payload: {repetitions}");
        builder.AppendLine(
            "- Targeted larger sample. A ten-run suite is smoke evidence and " +
            "cannot characterise a rate between the extremes.");
        builder.AppendLine();
        builder.AppendLine(
            "| Payload | Provenance | Selected | Rate | 95% interval | Latency med ms |");
        builder.AppendLine("|---|---|---:|---:|---|---:|");

        foreach (var id in new[] { "de-decretis", "de-decretis-enriched" })
        {
            var evaluationCase = all.Single(x => x.Id == id);

            var results = await harness.RepeatAsync(
                evaluationCase,
                repetitions,
                CancellationToken.None);

            var selected = results.Count(x => x.ExactMatch);
            var (lower, upper) = ProportionInterval.Wilson95(selected, results.Count);
            var latencies = results.Select(x => x.LatencyMilliseconds).Order().ToList();

            builder.AppendLine(
                $"| {id} | {evaluationCase.Source} | {selected}/{results.Count} " +
                $"| {(double)selected / results.Count:P0} " +
                $"| [{lower:P0}, {upper:P0}] " +
                $"| {latencies[latencies.Count / 2]:F0} |");
        }

        await WriteAsync("GENRE_FORM_VARIANCE_REPORT", "variance", builder.ToString());
    }

    [Fact]
    public async Task Independent_label_framing_is_probed()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = ModelName();
        var repetitions = Repetitions("GENRE_FORM_PROBE_REPETITIONS", 10);

        var harness = GenreFormEvaluationHarness.Create(model);
        var probe = new GenreFormApplicabilityProbe(harness.Runtime);
        var all = GenreFormEvaluationCase.Load();

        (string CaseId, string[] Terms)[] plan =
        [
            ("papacy-essay-enriched",
                ["Apologetic writings", "Essays", "Textbooks", "Academic theses"]),
            ("de-decretis", ["Apologetic writings"]),
            ("de-decretis-enriched", ["Apologetic writings"]),
            // Positive control: must stay affirmative.
            ("contra-gentes", ["Apologetic writings", "Textbooks"]),
            // Negative control: detects an over-affirming framing.
            ("bauckham-eyewitnesses", ["Apologetic writings", "Academic theses"])
        ];

        var builder = new StringBuilder();
        builder.AppendLine("# Independent-label framing — EVAL-4A");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{model}`");
        builder.AppendLine($"- Repetitions per question: {repetitions}");
        builder.AppendLine(
            "- Same policy rules, asked one term at a time instead of as a set.");
        builder.AppendLine();
        builder.AppendLine("| Case | Provenance | Term | Applies | Failures |");
        builder.AppendLine("|---|---|---|---:|---:|");

        foreach (var (caseId, terms) in plan)
        {
            var evaluationCase = all.Single(x => x.Id == caseId);

            foreach (var label in terms)
            {
                var term = harness.Policy.Terms.Single(x => x.PreferredLabel == label);
                var answers = new List<GenreFormApplicabilityResult>();

                for (var index = 0; index < repetitions; index++)
                {
                    answers.Add(
                        await probe.AskAsync(evaluationCase, term, CancellationToken.None));
                }

                builder.AppendLine(
                    $"| {caseId} | {evaluationCase.Source} | {label} " +
                    $"| {answers.Count(x => x.Applies)}/{answers.Count} " +
                    $"| {answers.Count(x => x.Failed)} |");
            }
        }

        await WriteAsync("GENRE_FORM_PROBE_REPORT", "probe", builder.ToString());
    }

    [Fact]
    public async Task Candidate_order_bias_is_probed()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = ModelName();
        var repetitions = Repetitions("GENRE_FORM_ORDER_REPETITIONS", 10);
        var all = GenreFormEvaluationCase.Load();

        (string Name, Func<IReadOnlyList<GenreFormPolicyTerm>,
            IReadOnlyList<GenreFormPolicyTerm>> Order)[] orders =
        [
            ("profile order", terms => terms),
            ("reversed", terms => terms.Reverse().ToList()),
            ("shuffled seed 1", terms => Shuffle(terms, 1)),
            ("shuffled seed 2", terms => Shuffle(terms, 2))
        ];

        var builder = new StringBuilder();
        builder.AppendLine("# Candidate-order bias — EVAL-4B");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{model}`");
        builder.AppendLine($"- Repetitions per ordering: {repetitions}");
        builder.AppendLine(
            "- The production prompt lists candidates in profile order, so " +
            "reordering the policy reorders the prompt with no code change.");
        builder.AppendLine();
        builder.AppendLine("| Case | Ordering | Exact | Apologetic writings | Essays |");
        builder.AppendLine("|---|---|---:|---:|---:|");

        foreach (var caseId in new[] { "papacy-essay-enriched", "contra-gentes" })
        {
            var evaluationCase = all.Single(x => x.Id == caseId);

            foreach (var (name, order) in orders)
            {
                var harness = GenreFormEvaluationHarness.CreateWithTermOrder(model, order);

                var results = await harness.RepeatAsync(
                    evaluationCase,
                    repetitions,
                    CancellationToken.None);

                builder.AppendLine(
                    $"| {caseId} | {name} " +
                    $"| {results.Count(x => x.ExactMatch)}/{results.Count} " +
                    $"| {Frequency(results, harness, "Apologetic writings")} " +
                    $"| {Frequency(results, harness, "Essays")} |");
            }
        }

        await WriteAsync("GENRE_FORM_ORDER_REPORT", "order", builder.ToString());
    }

    [Fact]
    public void Candidate_positions_under_each_ordering_are_reported()
    {
        // Deterministic: which of the two competing terms is listed later?
        var harness = GenreFormEvaluationHarness.Create("unused");

        (string Name, Func<IReadOnlyList<GenreFormPolicyTerm>,
            IReadOnlyList<GenreFormPolicyTerm>> Order)[] orders =
        [
            ("profile order", terms => terms),
            ("reversed", terms => terms.Reverse().ToList()),
            ("shuffled seed 1", terms => Shuffle(terms, 1)),
            ("shuffled seed 2", terms => Shuffle(terms, 2))
        ];

        foreach (var (name, order) in orders)
        {
            var selectable = order(harness.Policy.Terms)
                .Where(x => x.Usage == GenreFormPolicyUsage.Selectable)
                .ToList();

            var apologetic = selectable.FindIndex(
                x => x.PreferredLabel == "Apologetic writings");
            var essays = selectable.FindIndex(x => x.PreferredLabel == "Essays");

            Console.WriteLine(
                $"{name}: Apologetic writings at {apologetic}, " +
                $"Essays at {essays}, later = " +
                $"{(apologetic > essays ? "Apologetic writings" : "Essays")}");
        }
    }

    private static int Frequency(
        IReadOnlyList<GenreFormCaseResult> results,
        GenreFormEvaluationHarness harness,
        string label) =>
        results.Count(
            x => x.Suggested.Any(
                uri => string.Equals(harness.LabelFor(uri), label, StringComparison.Ordinal)));

    private static IReadOnlyList<GenreFormPolicyTerm> Shuffle(
        IReadOnlyList<GenreFormPolicyTerm> terms,
        int seed)
    {
        var random = new Random(seed);
        return terms.OrderBy(_ => random.Next()).ToList();
    }

    private static string ModelName() =>
        Environment.GetEnvironmentVariable("OLLAMA_GENRE_FORM_MODEL")
        ?? LocalModelEvaluationSupport.GetResponseModel();

    private static int Repetitions(string variable, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), out var value)
            ? value
            : fallback;

    private static async Task WriteAsync(
        string variable,
        string name,
        string markdown)
    {
        var output = Environment.GetEnvironmentVariable(variable)
                     ?? Path.Combine(
                         AppContext.BaseDirectory,
                         $"genre-form-{name}-report.md");

        await File.WriteAllTextAsync(output, markdown);
        Console.WriteLine(markdown);
        Console.WriteLine($"report written to {output}");
    }
}
