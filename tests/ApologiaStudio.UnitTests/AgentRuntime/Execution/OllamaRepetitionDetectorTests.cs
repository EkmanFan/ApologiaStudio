using System.Text;
using ApologiaStudio.AgentRuntime.Execution;

namespace ApologiaStudio.UnitTests.AgentRuntime.Execution;

public sealed class OllamaRepetitionDetectorTests
{
    [Fact]
    public void TryDetect_ShouldDetectRepeatedPhraseAtResponseEnd()
    {
        var content =
            "Les personnes sont " +
            string.Concat(
                Enumerable.Repeat(
                    "co-éternelles, ",
                    5));

        var detected =
            OllamaRepetitionDetector.TryDetect(
                content,
                out var match);

        Assert.True(detected);
        Assert.True(match.PatternLength >= 10);
        Assert.True(match.RepeatCount >= 4);
    }

    [Fact]
    public void TryDetect_ShouldIgnoreOrdinaryStructuredAnswer()
    {
        const string content =
            "Dieu est un en essence et trois en personnes. " +
            "Le Père est Dieu, le Fils est Dieu et le Saint-Esprit " +
            "est Dieu, sans être trois dieux.";

        var detected =
            OllamaRepetitionDetector.TryDetect(
                content,
                out _);

        Assert.False(detected);
    }

    [Fact]
    public void Guard_ShouldDetectDuringStreaming()
    {
        var builder =
            new StringBuilder(
                "Les personnes sont ");

        var guard =
            new OllamaRepetitionGuard();

        OllamaRepetitionMatch? match = null;

        for (var index = 0; index < 6; index++)
        {
            builder.Append("co-éternelles, ");

            if (guard.TryDetect(
                    builder,
                    out var detectedMatch))
            {
                match = detectedMatch;
                break;
            }
        }

        Assert.NotNull(match);
        Assert.True(match.RepeatCount >= 4);
    }
}
