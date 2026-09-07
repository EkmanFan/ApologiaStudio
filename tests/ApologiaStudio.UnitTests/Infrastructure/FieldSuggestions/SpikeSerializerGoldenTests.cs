using System.Text.Json;
using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

namespace ApologiaStudio.UnitTests.Infrastructure.FieldSuggestions;

/// <summary>
/// The serializer against real output of the original Spike serializer.
/// </summary>
/// <remarks>
/// The fixture holds 50 <c>content.serialized_input</c> values taken verbatim
/// from the gate-D split the two models were trained, validated and tested on:
/// 20 title-only records, 30 with a description, and 10 carrying the double
/// spaces or trailing whitespace the Spike never normalised.
///
/// Each is parsed back into its sections and re-serialized. Reproducing every
/// one byte for byte is the only evidence that Apologia feeds these models the
/// shape they were trained on.
/// </remarks>
public sealed class SpikeSerializerGoldenTests
{
    #region Methods

    [Fact]
    public void The_serializer_reproduces_every_recorded_spike_output()
    {
        var records = Load();

        Assert.Equal(50, records.Count);

        foreach (var expected in records)
        {
            var (title, description) = Parse(expected);

            Assert.Equal(
                expected,
                GenreFormEncoderSerializer.Serialize(
                    new FieldSuggestionEvidence(title, description)));
        }
    }

    [Fact]
    public void The_recorded_outputs_only_ever_use_two_sections()
    {
        // What the models actually saw. A third marker appearing here would
        // mean the fixture no longer describes the trained distribution.
        foreach (var record in Load())
        {
            Assert.DoesNotContain("[SUBTITLE]", record, StringComparison.Ordinal);
            Assert.DoesNotContain("[TOC]", record, StringComparison.Ordinal);
            Assert.DoesNotContain("[STRUCTURE]", record, StringComparison.Ordinal);
            Assert.StartsWith("[TITLE] ", record, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_fixture_covers_both_recorded_shapes_and_the_whitespace_cases()
    {
        var records = Load();

        Assert.Equal(20, records.Count(x => !x.Contains('\n')));
        Assert.Equal(30, records.Count(x => x.Contains('\n')));
        Assert.NotEmpty(
            records.Where(
                x => x.Contains("  ", StringComparison.Ordinal) ||
                     x.Split('\n').Any(s => s != s.TrimEnd())));
    }

    #endregion

    #region Methods Helpers

    private static List<string> Load()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Infrastructure",
            "FieldSuggestions",
            "spike-serializer-golden.jsonl");

        return File.ReadAllLines(path)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => JsonDocument.Parse(x)
                .RootElement
                .GetProperty("serializedInput")
                .GetString()!)
            .ToList();
    }

    /// <summary>
    /// Splits a recorded output back into the values the Spike serializer was
    /// given.
    /// </summary>
    private static (string? Title, string? Description) Parse(string serialized)
    {
        string? title = null;
        string? description = null;

        foreach (var section in serialized.Split('\n'))
        {
            if (section.StartsWith("[TITLE] ", StringComparison.Ordinal))
            {
                title = section["[TITLE] ".Length..];
            }
            else if (section.StartsWith("[DESCRIPTION] ", StringComparison.Ordinal))
            {
                description = section["[DESCRIPTION] ".Length..];
            }
            else
            {
                Assert.Fail($"Unexpected section '{section}'.");
            }
        }

        return (title, description);
    }

    #endregion
}
