namespace ApologiaStudio.Evaluations.GenreForm.Eval6;

/// <summary>
/// Guards the EVAL-6 benchmark identity. No model is loaded and no inference is
/// run: these assertions are what stop a campaign from silently benchmarking
/// the wrong split, the wrong scope or an invented definition.
/// </summary>
public sealed class Eval6ContractTests
{
    private const string SplitVariable = "EVAL6_TEST_SPLIT";

    [Fact]
    public void Machine_scope_is_the_twenty_four_authoritative_labels()
    {
        Assert.Equal(24, Eval6Scope.MachineLabels.Count);
        Assert.Equal(
            Eval6Scope.MachineLabels.Count,
            Eval6Scope.MachineLabels.Distinct(StringComparer.Ordinal).Count());

        // Manual-only product labels must never reach the machine benchmark.
        foreach (var manualOnly in new[]
                 {
                     "study_guide", "training_material", "instructional_lesson"
                 })
        {
            Assert.DoesNotContain(manualOnly, Eval6Scope.MachineLabels);
        }
    }

    [Fact]
    public void Every_label_carries_a_normative_definition()
    {
        var definitions = Eval6LabelDefinitions.Load(out var sha);

        Assert.NotEmpty(sha);

        foreach (var label in Eval6Scope.MachineLabels)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(definitions.Labels[label].Definition),
                $"'{label}' has no normative definition.");
        }
    }

    /// <summary>
    /// creed carries the broad V2.1 definition, not the superseded
    /// religious-only wording of the labelling policy.
    /// </summary>
    [Fact]
    public void Creed_uses_the_broad_definition()
    {
        var definitions = Eval6LabelDefinitions.Load(out _);
        var creed = definitions.Labels["creed"].Definition!;

        Assert.Contains("not restricted to religion", creed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("communauté religieuse", creed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frozen_split_yields_the_expected_decision_grid()
    {
        var path = Environment.GetEnvironmentVariable(SplitVariable);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            // The frozen split lives in the Spike Encoder repository and is not
            // vendored here; the assertion runs wherever it is available.
            return;
        }

        var records = Eval6Record.Load(path, out var sha);

        Assert.Equal(886, records.Count);
        Assert.NotEmpty(sha);
        Assert.All(records, x => Assert.False(
            string.IsNullOrWhiteSpace(x.Content.SerializedInput)));

        // Ground truth is encoder_labels, and it never leaves the machine scope.
        foreach (var label in records.SelectMany(x => x.EncoderLabels).Distinct())
        {
            Assert.Contains(label, Eval6Scope.MachineLabels);
        }

        Assert.Equal(
            records.Count(x => x.EncoderLabels.Count == 0),
            records.Count(x => x.IsOutOfTaxonomy));

        Assert.Equal(21_264, records.Count * Eval6Scope.MachineLabels.Count);
    }

    /// <summary>
    /// The stratified sample is the benchmark's identity. A duplicated decision
    /// would be scored twice, and a tier that replayed an earlier one would
    /// waste calls and bias the increment.
    /// </summary>
    [Fact]
    public void Stratified_sample_is_well_formed()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "GenreForm", "Eval6", "stratified-sample-v1.jsonl");

        var sample = Eval6SampleRow.Load(path, out var sha);

        Assert.NotEmpty(sha);
        Assert.Equal(1_434, sample.Count);
        Assert.Equal(24, sample.Select(x => x.Label).Distinct(StringComparer.Ordinal).Count());

        // Every decision identity appears exactly once, across all tiers.
        Assert.Equal(
            sample.Count,
            sample.Select(x => $"{x.RecordId}::{x.Label}").Distinct(StringComparer.Ordinal).Count());

        Assert.Equal(480, sample.Count(x => x.Tier == 1));
        Assert.Equal(960, sample.Count(x => x.Tier <= 2));
        Assert.Equal(1_434, sample.Count(x => x.Tier <= 3));

        foreach (var tier in new[] { 1, 2, 3 })
        {
            foreach (var group in sample.Where(x => x.Tier == tier).GroupBy(x => x.Label))
            {
                Assert.Equal(10, group.Count(x => !x.GroundTruth));
                Assert.InRange(group.Count(x => x.GroundTruth), 8, 10);
            }
        }
    }
}
