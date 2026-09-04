using System.Text;
using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.Evaluations.GenreForm;

/// <summary>
/// One response of one condition, reduced to what every condition can produce:
/// a set of selected terms, whether the deterministic contract accepted it, and
/// its cost. Condition C produces no single response of its own, so one C
/// response is the set of terms answered true across that repetition's
/// per-label calls.
/// </summary>
internal sealed record GenreFormConditionResponse(
    string CaseId,
    string Provenance,
    IReadOnlySet<string> Expected,
    IReadOnlySet<string> Selected,
    bool Valid,
    string? FailureDetail,
    double LatencyMilliseconds,
    int? PromptTokenCount,
    int? OutputTokenCount)
{
    public bool ExactMatch => Valid && Expected.SetEquals(Selected);

    public static GenreFormConditionResponse From(GenreFormCaseResult result) =>
        new(
            result.Case.Id,
            result.Case.Source,
            result.Expected,
            result.Suggested,
            result.Status == GenreFormCaseStatus.Classified,
            result.FailureDetail,
            result.LatencyMilliseconds,
            result.PromptTokenCount,
            result.OutputTokenCount);
}

/// <summary>
/// Scoring shared by every condition, so a difference between conditions is a
/// difference in behaviour and never in how it was measured.
///
/// Invalid responses are excluded from accuracy and reported on their own, the
/// same separation the baseline uses: a contract failure is not a wrong
/// classification.
/// </summary>
internal sealed class GenreFormFramingSummary(
    string condition,
    IReadOnlyList<GenreFormConditionResponse> responses,
    Func<string, string> label)
{
    private readonly List<GenreFormConditionResponse> _scored =
        responses.Where(x => x.Valid).ToList();

    public string Condition => condition;

    public int Total => responses.Count;

    public int Invalid => responses.Count(x => !x.Valid);

    public int Scored => _scored.Count;

    public int Exact => _scored.Count(x => x.ExactMatch);

    public int TruePositives =>
        _scored.Sum(x => x.Expected.Intersect(x.Selected).Count());

    public int FalsePositives =>
        _scored.Sum(x => x.Selected.Except(x.Expected).Count());

    public int FalseNegatives =>
        _scored.Sum(x => x.Expected.Except(x.Selected).Count());

    public double Precision =>
        TruePositives + FalsePositives == 0
            ? 0
            : (double)TruePositives / (TruePositives + FalsePositives);

    public double Recall =>
        TruePositives + FalseNegatives == 0
            ? 0
            : (double)TruePositives / (TruePositives + FalseNegatives);

    public double F1 =>
        Precision + Recall == 0 ? 0 : 2 * Precision * Recall / (Precision + Recall);

    public double MeanSelected =>
        _scored.Count == 0 ? 0 : _scored.Average(x => x.Selected.Count);

    /// <summary>
    /// Detects a framing that buys recall by simply saying true more often:
    /// how many terms it puts forward on cases whose reference is empty.
    /// </summary>
    public IReadOnlyList<GenreFormConditionResponse> NegativeControls =>
        _scored.Where(x => x.Expected.Count == 0).ToList();

    public string Row()
    {
        var controls = NegativeControls;
        var clean = controls.Count(x => x.Selected.Count == 0);
        var latencies = responses.Select(x => x.LatencyMilliseconds).Order().ToList();

        return
            $"| {condition} | {Exact}/{Scored} " +
            $"| {(Scored == 0 ? 0 : (double)Exact / Scored):P0} " +
            $"| {Precision:P0} | {Recall:P0} | {F1:P0} " +
            $"| {MeanSelected:F2} " +
            $"| {clean}/{controls.Count} " +
            $"| {Invalid} " +
            $"| {(latencies.Count == 0 ? 0 : latencies[latencies.Count / 2]):F0} " +
            $"| {Median(responses.Select(x => x.OutputTokenCount))} |";
    }

    public static string Header() =>
        "| Condition | Exact | Exact % | Precision | Recall | F1 | Mean labels " +
        "| Clean negatives | Invalid | Latency med ms | Out tokens med |\n" +
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|";

    /// <summary>
    /// Per-label counts, so a condition that improves one label while losing
    /// another cannot hide behind an aggregate.
    /// </summary>
    public void AppendPerLabel(StringBuilder builder)
    {
        var uris = _scored
            .SelectMany(x => x.Expected.Concat(x.Selected))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(label, StringComparer.Ordinal)
            .ToList();

        foreach (var uri in uris)
        {
            var truePositives = _scored.Count(
                x => x.Expected.Contains(uri) && x.Selected.Contains(uri));
            var falsePositives = _scored.Count(
                x => !x.Expected.Contains(uri) && x.Selected.Contains(uri));
            var falseNegatives = _scored.Count(
                x => x.Expected.Contains(uri) && !x.Selected.Contains(uri));

            builder.AppendLine(
                $"| {label(uri)} | {condition} | {truePositives} | {falsePositives} " +
                $"| {falseNegatives} " +
                $"| {Ratio(truePositives, truePositives + falsePositives)} " +
                $"| {Ratio(truePositives, truePositives + falseNegatives)} |");
        }
    }

    private static string Ratio(int numerator, int denominator) =>
        denominator == 0 ? "—" : $"{(double)numerator / denominator:P0}";

    private static string Median(IEnumerable<int?> values)
    {
        var ordered = values.Where(x => x is not null).Select(x => x!.Value).Order().ToList();
        return ordered.Count == 0 ? "—" : ordered[ordered.Count / 2].ToString();
    }
}
