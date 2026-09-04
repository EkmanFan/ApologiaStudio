using System.Text;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Evaluations.Support;

namespace ApologiaStudio.Evaluations.GenreForm;

/// <summary>
/// EVAL-5 — Independent Decision Matrix Framing.
///
/// Hypothesis: the production framing turns a multi-label classification into a
/// competitive selection of one dominant form. A framing that demands an
/// independent binary decision per candidate, while staying in a single
/// inference, should reduce that bias without multiplying cost by fourteen.
///
/// Three conditions over the same cases, the same evidence, the same candidate
/// list, the same model and the same deterministic policy validation:
///
/// A — current joint subset-selection framing (production behaviour).
/// B — independent decision matrix, one inference.
/// C — independent per-label calls, the EVAL-4 framing, used here as a
///     behavioural oracle and explicitly not as a proposed architecture.
///
/// Nothing in this file changes production prompt, service, policy, options,
/// model, persistence or UI.
///
/// Methodology, as established in EVAL-4: a ten-run suite is regression and
/// smoke evidence. A rate strictly between the extremes is reported with a
/// Wilson interval and comes from a targeted larger sample.
/// </summary>
public sealed class GenreFormFramingTests
{
    /// <summary>
    /// The required EVAL cases plus the controls needed to detect a framing
    /// that buys recall by answering true more often.
    /// </summary>
    private static readonly string[] ComparisonCases =
    [
        "habermas-resurrection",
        "septuagint",
        "contra-gentes",
        "de-decretis",
        "de-decretis-enriched",
        "papacy-essay",
        "papacy-essay-enriched",
        "bauckham-eyewitnesses",
        "calvin-institutes",
        "adversarial-study-of-sermons",
        "adversarial-translated-sacred-work",
        "adversarial-prompt-injection"
    ];

    /// <summary>
    /// Condition C costs one inference per candidate, so the full fourteen-term
    /// oracle runs on the subset where the multi-label question actually bites,
    /// plus the negative controls that keep it honest.
    /// </summary>
    private static readonly string[] OracleCases =
    [
        "contra-gentes",
        "de-decretis",
        "de-decretis-enriched",
        "papacy-essay-enriched",
        "septuagint",
        "bauckham-eyewitnesses",
        "adversarial-prompt-injection"
    ];

    /// <summary>
    /// Enough room for fourteen decisions and their justifications. Chosen from
    /// the observed truncation point, not tuned for quality.
    /// </summary>
    private const int WidenedMaximumOutputTokens = 2_400;

    [Fact]
    public async Task Joint_and_matrix_framings_are_compared()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = ModelName();
        var repetitions = Repetitions("EVAL5_REPETITIONS", 10);
        var all = GenreFormEvaluationCase.Load();

        var wideBudget = Repetitions(
            "EVAL5_MATRIX_OUTPUT_TOKENS",
            WidenedMaximumOutputTokens);

        var joint = GenreFormEvaluationHarness.Create(
            model,
            GenreFormConditions.JointSubsetSelection);

        var matrixClassifier = default(GenreFormDecisionMatrixClassifier);

        // B twice: once inside the output budget the product configures, once
        // inside a budget large enough to hold fourteen decisions. A framing
        // truncated into invalid JSON would otherwise be scored as a quality
        // result, and the budget is a real deployment constraint in its own
        // right.
        var matrixProduction = GenreFormEvaluationHarness.Create(
            model,
            GenreFormConditions.IndependentDecisionMatrix);

        var matrixWide = GenreFormEvaluationHarness.Create(
            model,
            (runtime, policy) => matrixClassifier =
                new GenreFormDecisionMatrixClassifier(
                    runtime,
                    policy,
                    new GenreFormClassificationValidator(),
                    TimeProvider.System),
            wideBudget);

        var responsesA = new List<GenreFormConditionResponse>();
        var responsesB = new List<GenreFormConditionResponse>();
        var responsesBWide = new List<GenreFormConditionResponse>();

        var perCase = new StringBuilder();
        perCase.AppendLine(
            "| Case | Provenance | Reference | A exact | A labels | B exact " +
            "| B labels | B-wide exact | B-wide labels | A invalid " +
            "| B invalid | B-wide invalid |");
        perCase.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var caseId in ComparisonCases)
        {
            var evaluationCase = all.Single(x => x.Id == caseId);

            var a = await CollectAsync(joint, evaluationCase, repetitions);
            var b = await CollectAsync(matrixProduction, evaluationCase, repetitions);
            var wide = await CollectAsync(matrixWide, evaluationCase, repetitions);

            responsesA.AddRange(a);
            responsesB.AddRange(b);
            responsesBWide.AddRange(wide);

            perCase.AppendLine(
                $"| {caseId} | {evaluationCase.Source} " +
                $"| {Describe(a[0].Expected, joint.LabelFor)} " +
                $"| {Rate(a, x => x.ExactMatch)} | {MeanLabels(a)} " +
                $"| {Rate(b, x => x.ExactMatch)} | {MeanLabels(b)} " +
                $"| {Rate(wide, x => x.ExactMatch)} | {MeanLabels(wide)} " +
                $"| {a.Count(x => !x.Valid)} | {b.Count(x => !x.Valid)} " +
                $"| {wide.Count(x => !x.Valid)} |");
        }

        var builder = new StringBuilder();
        builder.AppendLine("# Joint framing versus decision matrix — EVAL-5A");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{model}`");
        builder.AppendLine($"- Repetitions per case and condition: {repetitions}");
        builder.AppendLine($"- Cases: {ComparisonCases.Length}");
        builder.AppendLine(
            $"- Output budget: {GenreFormEvaluationHarness.ProductionMaximumOutputTokens} " +
            $"tokens for A and B, {wideBudget} tokens for B-wide.");
        builder.AppendLine(
            "- Every condition passes through the same deterministic policy " +
            "validation, so an invalid response is a contract failure in each " +
            "and is excluded from accuracy.");
        builder.AppendLine();

        builder.AppendLine("## Aggregate");
        builder.AppendLine();
        builder.AppendLine(GenreFormFramingSummary.Header());

        var summaryA = new GenreFormFramingSummary("A joint", responsesA, joint.LabelFor);
        var summaryB = new GenreFormFramingSummary("B matrix", responsesB, joint.LabelFor);
        var summaryWide = new GenreFormFramingSummary(
            "B-wide matrix", responsesBWide, joint.LabelFor);

        builder.AppendLine(summaryA.Row());
        builder.AppendLine(summaryB.Row());
        builder.AppendLine(summaryWide.Row());
        builder.AppendLine();

        builder.AppendLine("## Invalid responses by cause");
        builder.AppendLine();
        builder.AppendLine("| Condition | Cause | Count |");
        builder.AppendLine("|---|---|---:|");
        AppendFailures(builder, "A joint", responsesA);
        AppendFailures(builder, "B matrix", responsesB);
        AppendFailures(builder, "B-wide matrix", responsesBWide);
        builder.AppendLine();

        builder.AppendLine("## Per case");
        builder.AppendLine();
        builder.Append(perCase);
        builder.AppendLine();

        builder.AppendLine("## Per label");
        builder.AppendLine();
        builder.AppendLine("| Label | Condition | TP | FP | FN | Precision | Recall |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        summaryA.AppendPerLabel(builder);
        summaryB.AppendPerLabel(builder);
        summaryWide.AppendPerLabel(builder);
        builder.AppendLine();

        AppendCoverage(builder, matrixClassifier);

        await WriteAsync("EVAL5_COMPARISON_REPORT", "comparison", builder.ToString());
    }

    /// <summary>
    /// The EVAL-4B permutations replayed under both framings. The question is
    /// whether B keeps Apologetic writings and Essays true together whatever
    /// the candidate order.
    /// </summary>
    [Fact]
    public async Task Candidate_order_sensitivity_is_compared()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = ModelName();
        var repetitions = Repetitions("EVAL5_ORDER_REPETITIONS", 10);
        var all = GenreFormEvaluationCase.Load();

        (string Name, Func<IReadOnlyList<GenreFormPolicyTerm>,
            IReadOnlyList<GenreFormPolicyTerm>> Order)[] orders =
        [
            ("profile order", terms => terms),
            ("reversed", terms => terms.Reverse().ToList()),
            ("shuffled seed 1", terms => Shuffle(terms, 1)),
            ("shuffled seed 2", terms => Shuffle(terms, 2))
        ];

        (string Name, GenreFormClassifierFactory Condition)[] conditions =
        [
            ("A joint", GenreFormConditions.JointSubsetSelection),
            ("B-wide matrix", GenreFormConditions.IndependentDecisionMatrix)
        ];

        var builder = new StringBuilder();
        builder.AppendLine("# Candidate-order sensitivity by framing — EVAL-5B");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{model}`");
        builder.AppendLine($"- Repetitions per ordering and condition: {repetitions}");
        builder.AppendLine(
            "- Same permutations as EVAL-4B. Reordering the policy reorders " +
            "the candidate list of every condition identically.");
        builder.AppendLine(
            $"- The matrix condition runs at the widened {WidenedMaximumOutputTokens}-token " +
            "budget, so an ordering effect is never confused with truncation.");
        builder.AppendLine();
        builder.AppendLine(
            "| Case | Condition | Ordering | Exact | Apologetic writings " +
            "| Essays | Both | Invalid |");
        builder.AppendLine("|---|---|---|---:|---:|---:|---:|---:|");

        foreach (var caseId in new[] { "papacy-essay-enriched", "contra-gentes" })
        {
            var evaluationCase = all.Single(x => x.Id == caseId);

            foreach (var (conditionName, condition) in conditions)
            {
                foreach (var (orderName, order) in orders)
                {
                    var harness = GenreFormEvaluationHarness.CreateWithTermOrder(
                        model,
                        order,
                        condition,
                        WidenedMaximumOutputTokens);

                    var results = await harness.RepeatAsync(
                        evaluationCase,
                        repetitions,
                        CancellationToken.None);

                    var apologetic = Frequency(results, harness, "Apologetic writings");
                    var essays = Frequency(results, harness, "Essays");
                    var both = results.Count(
                        x => Has(x, harness, "Apologetic writings") &&
                             Has(x, harness, "Essays"));

                    builder.AppendLine(
                        $"| {caseId} | {conditionName} | {orderName} " +
                        $"| {results.Count(x => x.ExactMatch)}/{results.Count} " +
                        $"| {apologetic} | {essays} | {both} " +
                        $"| {results.Count(x => x.Status != GenreFormCaseStatus.Classified)} |");
                }
            }
        }

        await WriteAsync("EVAL5_ORDER_REPORT", "order", builder.ToString());
    }

    /// <summary>
    /// De Decretis under all three framings. EVAL-4 measured A at 20% over
    /// fifty runs and C at 10/10; this places B between or beside them.
    /// </summary>
    [Fact]
    public async Task De_decretis_rate_is_compared_across_framings()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = ModelName();
        var repetitions = Repetitions("EVAL5_RATE_REPETITIONS", 30);
        var all = GenreFormEvaluationCase.Load();

        var builder = new StringBuilder();
        builder.AppendLine("# De Decretis selection rate by framing — EVAL-5C");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{model}`");
        builder.AppendLine($"- Repetitions per payload and condition: {repetitions}");
        builder.AppendLine(
            "- Targeted larger sample with Wilson 95% intervals. Rates whose " +
            "intervals overlap are not distinguished by this experiment.");
        builder.AppendLine(
            $"- The matrix condition runs at the widened {WidenedMaximumOutputTokens}-token " +
            "budget.");
        builder.AppendLine();
        builder.AppendLine(
            "| Payload | Provenance | Condition | Apologetic writings | Rate " +
            "| 95% interval | Invalid | Latency med ms |");
        builder.AppendLine("|---|---|---|---:|---:|---|---:|---:|");

        foreach (var caseId in new[] { "de-decretis", "de-decretis-enriched" })
        {
            var evaluationCase = all.Single(x => x.Id == caseId);

            foreach (var (name, condition) in new (string, GenreFormClassifierFactory)[]
                     {
                         ("A joint", GenreFormConditions.JointSubsetSelection),
                         ("B-wide matrix", GenreFormConditions.IndependentDecisionMatrix)
                     })
            {
                var harness = GenreFormEvaluationHarness.Create(
                    model,
                    condition,
                    WidenedMaximumOutputTokens);

                var results = await harness.RepeatAsync(
                    evaluationCase,
                    repetitions,
                    CancellationToken.None);

                var selected = results.Count(
                    x => Has(x, harness, "Apologetic writings"));

                AppendRate(
                    builder,
                    caseId,
                    evaluationCase.Source,
                    name,
                    selected,
                    results.Count,
                    results.Count(x => x.Status != GenreFormCaseStatus.Classified),
                    results.Select(x => x.LatencyMilliseconds));
            }

            var oracle = GenreFormEvaluationHarness.Create(model);
            var probe = new GenreFormApplicabilityProbe(oracle.Runtime);
            var term = oracle.Policy.Terms.Single(
                x => x.PreferredLabel == "Apologetic writings");

            var answers = new List<GenreFormApplicabilityResult>();

            for (var index = 0; index < repetitions; index++)
            {
                answers.Add(
                    await probe.AskAsync(evaluationCase, term, CancellationToken.None));
            }

            AppendRate(
                builder,
                caseId,
                evaluationCase.Source,
                "C per-label",
                answers.Count(x => x.Applies),
                answers.Count,
                answers.Count(x => x.Failed),
                answers.Select(x => x.LatencyMilliseconds));
        }

        await WriteAsync("EVAL5_RATE_REPORT", "rate", builder.ToString());
    }

    /// <summary>
    /// The full fourteen-term oracle. Each repetition asks every candidate
    /// separately; the terms answered true in that repetition form one C
    /// response, which is then scored exactly like an A or B response.
    /// </summary>
    [Fact]
    public async Task Independent_label_oracle_matrix_is_measured()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = ModelName();
        var repetitions = Repetitions("EVAL5_ORACLE_REPETITIONS", 3);
        var all = GenreFormEvaluationCase.Load();

        var harness = GenreFormEvaluationHarness.Create(model);
        var probe = new GenreFormApplicabilityProbe(harness.Runtime);

        var selectable = harness.Policy.SelectableTerms.ToList();
        var responses = new List<GenreFormConditionResponse>();

        var matrix = new StringBuilder();
        matrix.AppendLine("| Case | Provenance | Term | True | Reference |");
        matrix.AppendLine("|---|---|---|---:|---|");

        foreach (var caseId in OracleCases)
        {
            var evaluationCase = all.Single(x => x.Id == caseId);
            var expected = Expected(evaluationCase, harness);

            // Repetition outer, candidate inner: one pass is one C response.
            var perRepetition = new List<(HashSet<string> Selected, double Latency, int Tokens, bool Failed)>();

            for (var index = 0; index < repetitions; index++)
            {
                perRepetition.Add(
                    (new HashSet<string>(StringComparer.Ordinal), 0, 0, false));
            }

            var trueCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var index = 0; index < repetitions; index++)
            {
                foreach (var term in selectable)
                {
                    var answer = await probe.AskAsync(
                        evaluationCase,
                        term,
                        CancellationToken.None);

                    var entry = perRepetition[index];

                    if (answer.Applies)
                    {
                        entry.Selected.Add(term.AuthorityUri);
                        trueCounts[term.AuthorityUri] =
                            trueCounts.GetValueOrDefault(term.AuthorityUri) + 1;
                    }

                    perRepetition[index] = (
                        entry.Selected,
                        entry.Latency + answer.LatencyMilliseconds,
                        entry.Tokens + (answer.OutputTokenCount ?? 0),
                        entry.Failed || answer.Failed);
                }
            }

            foreach (var (selected, latency, tokens, failed) in perRepetition)
            {
                responses.Add(new GenreFormConditionResponse(
                    caseId,
                    evaluationCase.Source,
                    expected,
                    selected,
                    !failed,
                    failed ? "probe failure" : null,
                    latency,
                    null,
                    tokens));
            }

            foreach (var term in selectable)
            {
                var count = trueCounts.GetValueOrDefault(term.AuthorityUri);

                if (count == 0)
                {
                    continue;
                }

                matrix.AppendLine(
                    $"| {caseId} | {evaluationCase.Source} | {term.PreferredLabel} " +
                    $"| {count}/{repetitions} " +
                    $"| {(expected.Contains(term.AuthorityUri) ? "expected" : "not expected")} |");
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine("# Independent per-label oracle — EVAL-5D");
        builder.AppendLine();
        builder.AppendLine($"- Model: `{model}`");
        builder.AppendLine($"- Repetitions per case: {repetitions}");
        builder.AppendLine($"- Candidates asked per repetition: {selectable.Count}");
        builder.AppendLine($"- Inferences: {OracleCases.Length * selectable.Count * repetitions}");
        builder.AppendLine(
            "- C is a behavioural oracle, not a proposed architecture. Its " +
            "responses are fourteen separate binary answers and are therefore " +
            "not subject to the joint-response contract; the hierarchy and " +
            "cardinality guards that constrain A and B do not constrain C.");
        builder.AppendLine();
        builder.AppendLine("## Aggregate on the oracle case set");
        builder.AppendLine();
        builder.AppendLine(GenreFormFramingSummary.Header());
        builder.AppendLine(
            new GenreFormFramingSummary("C per-label", responses, harness.LabelFor).Row());
        builder.AppendLine();
        builder.AppendLine(
            "Compare against conditions A and B restricted to the same cases " +
            "in the EVAL-5A report.");
        builder.AppendLine();
        builder.AppendLine("## Terms answered true at least once");
        builder.AppendLine();
        builder.Append(matrix);

        await WriteAsync("EVAL5_ORACLE_REPORT", "oracle", builder.ToString());
    }

    private static async Task<List<GenreFormConditionResponse>> CollectAsync(
        GenreFormEvaluationHarness harness,
        GenreFormEvaluationCase evaluationCase,
        int repetitions) =>
        (await harness.RepeatAsync(evaluationCase, repetitions, CancellationToken.None))
        .Select(GenreFormConditionResponse.From)
        .ToList();

    private static void AppendFailures(
        StringBuilder builder,
        string condition,
        IReadOnlyList<GenreFormConditionResponse> responses)
    {
        var causes = responses
            .Where(x => !x.Valid)
            .GroupBy(x => x.FailureDetail ?? "unspecified", StringComparer.Ordinal)
            .OrderByDescending(x => x.Count());

        var any = false;

        foreach (var cause in causes)
        {
            any = true;
            builder.AppendLine($"| {condition} | {cause.Key} | {cause.Count()} |");
        }

        if (!any)
        {
            builder.AppendLine($"| {condition} | none | 0 |");
        }
    }

    private static void AppendCoverage(
        StringBuilder builder,
        GenreFormDecisionMatrixClassifier? classifier)
    {
        if (classifier is null || classifier.Observations.Count == 0)
        {
            return;
        }

        var observations = classifier.Observations;

        builder.AppendLine("## Condition B matrix coverage");
        builder.AppendLine();
        builder.AppendLine(
            "Coverage is a property of this framing rather than of the " +
            "vocabulary: a model that silently omits candidates is not " +
            "answering the question that was asked.");
        builder.AppendLine();
        builder.AppendLine(
            $"- Responses: {observations.Count}");
        builder.AppendLine(
            "- Covering every candidate exactly once: " +
            $"{observations.Count(x => x.CoversEveryCandidate)}/{observations.Count}");
        builder.AppendLine(
            $"- Median decisions returned: {Median(observations.Select(x => x.DecisionCount))}");
        builder.AppendLine(
            $"- Responses with an unknown identifier: {observations.Count(x => x.UnknownIdentifiers > 0)}");
        builder.AppendLine(
            $"- Responses with a duplicated candidate: {observations.Count(x => x.DuplicateIdentifiers > 0)}");
        builder.AppendLine(
            $"- Mean true decisions: {observations.Average(x => (double)x.TrueCount):F2}");
        builder.AppendLine();
    }

    private static void AppendRate(
        StringBuilder builder,
        string caseId,
        string provenance,
        string condition,
        int selected,
        int trials,
        int invalid,
        IEnumerable<double> latencies)
    {
        var (lower, upper) = ProportionInterval.Wilson95(selected, trials);
        var ordered = latencies.Order().ToList();

        builder.AppendLine(
            $"| {caseId} | {provenance} | {condition} | {selected}/{trials} " +
            $"| {(trials == 0 ? 0 : (double)selected / trials):P0} " +
            $"| [{lower:P0}, {upper:P0}] | {invalid} " +
            $"| {(ordered.Count == 0 ? 0 : ordered[ordered.Count / 2]):F0} |");
    }

    private static IReadOnlySet<string> Expected(
        GenreFormEvaluationCase evaluationCase,
        GenreFormEvaluationHarness harness) =>
        evaluationCase.Expected
            .Select(x => GenreFormSelectionRules.Resolve(x, harness.Policy)?.AuthorityUri)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToHashSet(StringComparer.Ordinal);

    private static string Describe(
        IReadOnlySet<string> terms,
        Func<string, string> label) =>
        terms.Count == 0
            ? "∅"
            : string.Join(", ", terms.Select(label).Order(StringComparer.Ordinal));

    private static string Rate(
        IReadOnlyList<GenreFormConditionResponse> responses,
        Func<GenreFormConditionResponse, bool> predicate) =>
        $"{responses.Count(predicate)}/{responses.Count}";

    private static string MeanLabels(
        IReadOnlyList<GenreFormConditionResponse> responses)
    {
        var scored = responses.Where(x => x.Valid).ToList();
        return scored.Count == 0 ? "—" : $"{scored.Average(x => x.Selected.Count):F2}";
    }

    private static bool Has(
        GenreFormCaseResult result,
        GenreFormEvaluationHarness harness,
        string label) =>
        result.Status == GenreFormCaseStatus.Classified &&
        result.Suggested.Any(
            uri => string.Equals(harness.LabelFor(uri), label, StringComparison.Ordinal));

    private static int Frequency(
        IReadOnlyList<GenreFormCaseResult> results,
        GenreFormEvaluationHarness harness,
        string label) =>
        results.Count(x => Has(x, harness, label));

    private static IReadOnlyList<GenreFormPolicyTerm> Shuffle(
        IReadOnlyList<GenreFormPolicyTerm> terms,
        int seed)
    {
        var random = new Random(seed);
        return terms.OrderBy(_ => random.Next()).ToList();
    }

    private static string Median(IEnumerable<int> values)
    {
        var ordered = values.Order().ToList();
        return ordered.Count == 0 ? "—" : ordered[ordered.Count / 2].ToString();
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
                         $"genre-form-eval5-{name}-report.md");

        await File.WriteAllTextAsync(output, markdown);
        Console.WriteLine(markdown);
        Console.WriteLine($"report written to {output}");
    }
}
