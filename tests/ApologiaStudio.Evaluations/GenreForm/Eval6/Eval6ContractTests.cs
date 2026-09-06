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
}
