using System.Text;
using ApologiaStudio.Evaluations.Support;

namespace ApologiaStudio.Evaluations.GenreForm.Eval6;

/// <summary>
/// EVAL-6 entry points. Both are doubly gated: the usual local-model switch,
/// plus a dedicated one, so a campaign of 21 264 inferences can never start as
/// a side effect of running the normal test suite.
/// </summary>
public sealed class Eval6CampaignTests
{
    private const string RunGate = "EVAL6_RUN";

    private const string CalibrationGate = "EVAL6_CALIBRATION";

    [Fact]
    public async Task Llm_per_label_campaign_runs()
    {
        if (!Enabled(RunGate))
        {
            return;
        }

        var summary = await new Eval6Campaign(Options(null))
            .RunAsync(CancellationToken.None);

        Report("EVAL-6 campaign", summary);
    }

    /// <summary>
    /// Small stratified probe whose only purpose is to turn the duration
    /// estimate into a measurement before committing several hours.
    /// </summary>
    [Fact]
    public async Task Llm_per_label_calibration_runs()
    {
        if (!Enabled(CalibrationGate))
        {
            return;
        }

        var decisions = Number("EVAL6_CALIBRATION_DECISIONS", 240);

        var summary = await new Eval6Campaign(Options(decisions))
            .RunAsync(CancellationToken.None);

        Report("EVAL-6 calibration", summary);
    }

    private static Eval6Options Options(int? maximumDecisions) =>
        new(
            Environment.GetEnvironmentVariable("EVAL6_TEST_SPLIT")
            ?? throw new InvalidOperationException(
                "EVAL6_TEST_SPLIT must point at the frozen Spike Encoder V2.1 test split."),
            Environment.GetEnvironmentVariable("EVAL6_OUTPUT_DIR")
            ?? throw new InvalidOperationException("EVAL6_OUTPUT_DIR must be set."),
            Environment.GetEnvironmentVariable("EVAL6_MODEL") ?? "qwen3.8:27b",
            0.2,
            Number("EVAL6_MAX_OUTPUT_TOKENS", 64),
            Number("EVAL6_TIMEOUT_SECONDS", 120),
            Number("EVAL6_MAX_ATTEMPTS", 3),
            Environment.GetEnvironmentVariable("EVAL6_KEEP_ALIVE") ?? "30m",
            string.Equals(
                Environment.GetEnvironmentVariable("EVAL6_RETRY_UNRESOLVED"),
                "true",
                StringComparison.OrdinalIgnoreCase),
            maximumDecisions);

    private static bool Enabled(string gate) =>
        LocalModelEvaluationSupport.IsEnabled() &&
        string.Equals(
            Environment.GetEnvironmentVariable(gate),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static int Number(string variable, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), out var value)
            ? value
            : fallback;

    private static void Report(string title, Eval6CampaignSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine($"- decisions in the grid: {summary.TotalDecisions}");
        builder.AppendLine($"- already recorded on entry: {summary.AlreadyRecorded} " +
                           $"(ok {summary.ResumedOk}, unresolved {summary.ResumedUnresolved})");
        builder.AppendLine($"- executed this run: {summary.Executed}");
        builder.AppendLine($"- ok {summary.Ok} / invalid {summary.Invalid} / failed {summary.Failed}");
        Console.WriteLine(builder.ToString());
    }
}
