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

        Assert.Equal(11, cases.Count(x => x.Kind == "reference"));
        Assert.NotEmpty(cases.Where(x => x.Kind == "adversarial"));

        Assert.All(
            cases,
            x =>
            {
                Assert.False(string.IsNullOrWhiteSpace(x.Id));
                Assert.False(string.IsNullOrWhiteSpace(x.Title));
                Assert.Equal("curated", x.Source);
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
}
