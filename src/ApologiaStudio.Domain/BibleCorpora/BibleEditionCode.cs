using System.Text.RegularExpressions;

namespace ApologiaStudio.Domain.BibleCorpora;

public readonly partial record struct BibleEditionCode
{
    public BibleEditionCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 64 || !ValidCodeRegex().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Bible edition code must contain lowercase letters, digits, and single hyphens only.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidCodeRegex();
}
