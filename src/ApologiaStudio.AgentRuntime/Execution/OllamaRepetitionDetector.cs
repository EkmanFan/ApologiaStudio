using System.Text;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed record OllamaRepetitionMatch(
    int PatternLength,
    int RepeatCount);

public static class OllamaRepetitionDetector
{
    private const int MinimumPatternLength = 10;
    private const int MaximumPatternLength = 256;
    private const int RequiredRepeatCount = 4;
    private const int MaximumInspectedCharacters =
        MaximumPatternLength * RequiredRepeatCount + 256;

    public static bool TryDetect(
        string? content,
        out OllamaRepetitionMatch match)
    {
        match = null!;

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var inspectedStart =
            Math.Max(
                0,
                content.Length - MaximumInspectedCharacters);

        var normalized =
            Normalize(
                content.AsSpan(inspectedStart));

        if (normalized.Length <
            MinimumPatternLength * RequiredRepeatCount)
        {
            return false;
        }

        var maximumPatternLength =
            Math.Min(
                MaximumPatternLength,
                normalized.Length / RequiredRepeatCount);

        var normalizedSpan =
            normalized.AsSpan();

        for (var patternLength = MinimumPatternLength;
             patternLength <= maximumPatternLength;
             patternLength++)
        {
            var suffixStart =
                normalized.Length - patternLength;

            var pattern =
                normalizedSpan.Slice(
                    suffixStart,
                    patternLength);

            if (!ContainsEnoughSignal(pattern))
            {
                continue;
            }

            var repeatCount = 1;
            var candidateStart =
                suffixStart - patternLength;

            while (candidateStart >= 0 &&
                   normalizedSpan
                       .Slice(
                           candidateStart,
                           patternLength)
                       .SequenceEqual(pattern))
            {
                repeatCount++;
                candidateStart -= patternLength;
            }

            if (repeatCount < RequiredRepeatCount)
            {
                continue;
            }

            match =
                new OllamaRepetitionMatch(
                    patternLength,
                    repeatCount);

            return true;
        }

        return false;
    }

    private static string Normalize(
        ReadOnlySpan<char> content)
    {
        var builder =
            new StringBuilder(content.Length);

        var previousWasWhitespace = false;

        foreach (var character in content)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(
                char.ToLowerInvariant(character));

            previousWasWhitespace = false;
        }

        return builder
            .ToString()
            .Trim();
    }

    private static bool ContainsEnoughSignal(
        ReadOnlySpan<char> pattern)
    {
        var signalCharacters = 0;

        foreach (var character in pattern)
        {
            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            signalCharacters++;

            if (signalCharacters >= 4)
            {
                return true;
            }
        }

        return false;
    }
}
public sealed class OllamaRepetitionGuard
{
    private const int MinimumCharactersBetweenChecks = 8;

    private int _lastCheckedLength;

    public bool TryDetect(
        StringBuilder content,
        out OllamaRepetitionMatch match)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length - _lastCheckedLength <
            MinimumCharactersBetweenChecks)
        {
            match = null!;
            return false;
        }

        _lastCheckedLength = content.Length;

        return OllamaRepetitionDetector.TryDetect(
            content.ToString(),
            out match);
    }
}