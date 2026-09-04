using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Evaluations.Support;

namespace ApologiaStudio.Evaluations.GenreForm;

/// <summary>
/// Baseline measurement for the Genre/Form vertical slice. Establishes what
/// the current model, prompt and policy actually do before anything is tuned.
/// </summary>
public sealed class GenreFormEvaluationTests
{
    [Fact]
    public void Reference_and_adversarial_cases_are_well_formed()
    {
        // Deterministic: the case set is checked without any model.
        var cases = GenreFormEvaluationCase.Load();

        // The eleven approved reference cases are the immutable corpus;
        // enrichment variants are carried alongside, never in place of them.
        Assert.Equal(11, cases.Count(x => x.Kind == "reference"));
        Assert.NotEmpty(cases.Where(x => x.Kind == "adversarial"));
        Assert.NotEmpty(cases.Where(x => x.Kind == "enrichment"));

        Assert.All(
            cases,
            x =>
            {
                Assert.False(string.IsNullOrWhiteSpace(x.Id));
                Assert.False(string.IsNullOrWhiteSpace(x.Title));

                // Curated and source-supported evidence must stay
                // distinguishable: a conclusion drawn from the work itself is
                // not the same kind of evidence as one written for the test.
                Assert.Contains(x.Source, new[] { "curated", "source-supported" });
            });

        Assert.Equal(
            cases.Select(x => x.Id).Distinct().Count(),
            cases.Count);
    }

    [Fact]
    public async Task Genre_form_baseline_is_measured()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = Environment.GetEnvironmentVariable("OLLAMA_GENRE_FORM_MODEL")
                    ?? LocalModelEvaluationSupport.GetResponseModel();

        var harness = GenreFormEvaluationHarness.Create(model);
        var report = await harness.RunAsync(
            GenreFormEvaluationCase.Load(),
            CancellationToken.None);

        var markdown = report.ToMarkdown(harness.LabelFor);

        var output = Environment.GetEnvironmentVariable("GENRE_FORM_EVALUATION_REPORT")
                     ?? Path.Combine(
                         AppContext.BaseDirectory,
                         "genre-form-evaluation-report.md");

        await File.WriteAllTextAsync(output, markdown);

        Console.WriteLine(markdown);
        Console.WriteLine($"report written to {output}");

        // The baseline records what happens; it does not gate on quality.
        Assert.Equal(
            GenreFormEvaluationCase.Load().Count,
            report.Results.Count);
    }

    /// <summary>
    /// The four cases that missed or hesitated at baseline, plus easy positive
    /// and expected-empty controls. Controls tell a genuinely unstable model
    /// apart from cases that are specifically hard.
    /// </summary>
    private static readonly string[] StabilityCaseIds =
    [
        "de-decretis",
        "contra-gentes",
        "papacy-essay",
        "adversarial-translated-sacred-work",
        "habermas-resurrection",
        "septuagint",
        "bauckham-eyewitnesses",
        "adversarial-prompt-injection"
    ];

    [Fact]
    public async Task Genre_form_stability_is_measured()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = Environment.GetEnvironmentVariable("OLLAMA_GENRE_FORM_MODEL")
                    ?? LocalModelEvaluationSupport.GetResponseModel();

        var repetitions = int.TryParse(
            Environment.GetEnvironmentVariable("GENRE_FORM_STABILITY_REPETITIONS"),
            out var configured)
            ? configured
            : 10;

        var harness = GenreFormEvaluationHarness.Create(model);
        var all = GenreFormEvaluationCase.Load();

        var cases = new List<GenreFormStabilityCase>();

        foreach (var id in StabilityCaseIds)
        {
            var evaluationCase = all.Single(x => x.Id == id);

            var results = await harness.RepeatAsync(
                evaluationCase,
                repetitions,
                CancellationToken.None);

            cases.Add(new GenreFormStabilityCase(
                evaluationCase,
                results[0].Expected,
                results));
        }

        var report = new GenreFormStabilityReport(
            model,
            StructuredGenreFormClassifier.PromptVersion,
            GenreFormProfile.Version,
            repetitions,
            DateTimeOffset.UtcNow,
            cases);

        var markdown = report.ToMarkdown(harness.LabelFor);

        var output = Environment.GetEnvironmentVariable("GENRE_FORM_STABILITY_REPORT")
                     ?? Path.Combine(
                         AppContext.BaseDirectory,
                         "genre-form-stability-report.md");

        await File.WriteAllTextAsync(output, markdown);

        Console.WriteLine(markdown);
        Console.WriteLine($"report written to {output}");

        Assert.Equal(StabilityCaseIds.Length, report.Cases.Count);
    }

    /// <summary>
    /// EVAL-3: only the evidence changes. Same model, prompt, policy, options,
    /// validator and repetition count as EVAL-2, so the delta is attributable
    /// to the payload alone. Controls are carried unchanged so a recall gain
    /// bought with false positives would be visible.
    /// </summary>
    private static readonly string[] EnrichmentCaseIds =
    [
        "de-decretis",
        "de-decretis-enriched",
        "papacy-essay",
        "papacy-essay-enriched",
        "contra-gentes",
        "bauckham-eyewitnesses"
    ];

    [Fact]
    public async Task Genre_form_evidence_enrichment_is_measured()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            return;
        }

        var model = Environment.GetEnvironmentVariable("OLLAMA_GENRE_FORM_MODEL")
                    ?? LocalModelEvaluationSupport.GetResponseModel();

        var repetitions = int.TryParse(
            Environment.GetEnvironmentVariable("GENRE_FORM_STABILITY_REPETITIONS"),
            out var configured)
            ? configured
            : 10;

        var harness = GenreFormEvaluationHarness.Create(model);
        var all = GenreFormEvaluationCase.Load();

        var cases = new List<GenreFormStabilityCase>();

        foreach (var id in EnrichmentCaseIds)
        {
            var evaluationCase = all.Single(x => x.Id == id);

            var results = await harness.RepeatAsync(
                evaluationCase,
                repetitions,
                CancellationToken.None);

            cases.Add(new GenreFormStabilityCase(
                evaluationCase,
                results[0].Expected,
                results));
        }

        var report = new GenreFormStabilityReport(
            model,
            StructuredGenreFormClassifier.PromptVersion,
            GenreFormProfile.Version,
            repetitions,
            DateTimeOffset.UtcNow,
            cases);

        var markdown = report.ToMarkdown(harness.LabelFor);

        var output = Environment.GetEnvironmentVariable("GENRE_FORM_ENRICHMENT_REPORT")
                     ?? Path.Combine(
                         AppContext.BaseDirectory,
                         "genre-form-enrichment-report.md");

        await File.WriteAllTextAsync(output, markdown);

        Console.WriteLine(markdown);
        Console.WriteLine($"report written to {output}");

        Assert.Equal(EnrichmentCaseIds.Length, report.Cases.Count);
    }

    [Fact]
    public void Enriched_evidence_never_names_the_expected_label()
    {
        // The payload must describe the work, not tell the model the answer.
        var enriched = GenreFormEvaluationCase.Load()
            .Where(x => x.Kind == "enrichment")
            .ToList();

        Assert.NotEmpty(enriched);

        string[] forbidden =
        [
            "apologetic", "apologétique", "apologie",
            "essay", "essai",
            "academic thesis", "textbook", "sacred work"
        ];

        foreach (var evaluationCase in enriched)
        {
            var payload = string.Join(
                " ",
                new[] { evaluationCase.Title, evaluationCase.Description }
                    .Concat(evaluationCase.Sections.Select(x => x.Text)))
                .ToLowerInvariant();

            foreach (var term in forbidden)
            {
                Assert.DoesNotContain(term, payload, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void De_decretis_and_contra_gentes_differ_only_in_what_is_defended()
    {
        // Deterministic input comparison: no model involved.
        var cases = GenreFormEvaluationCase.Load();
        var deDecretis = cases.Single(x => x.Id == "de-decretis");
        var contraGentes = cases.Single(x => x.Id == "contra-gentes");

        Assert.Equal(contraGentes.Contributors, deDecretis.Contributors);
        Assert.Equal(contraGentes.LanguageCode, deDecretis.LanguageCode);
        Assert.Null(deDecretis.EditionStatement);
        Assert.Null(contraGentes.EditionStatement);

        // Both describe a defence; they differ in its object.
        Assert.Contains("defence", deDecretis.Description!, StringComparison.Ordinal);
        Assert.Contains("defence", contraGentes.Description!, StringComparison.Ordinal);
        Assert.Contains("Council of Nicaea", deDecretis.Description!, StringComparison.Ordinal);
        Assert.Contains("Christian faith", contraGentes.Description!, StringComparison.Ordinal);
    }
}
