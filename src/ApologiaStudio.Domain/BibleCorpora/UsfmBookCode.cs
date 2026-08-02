using System.Text.RegularExpressions;

namespace ApologiaStudio.Domain.BibleCorpora;

public readonly partial record struct UsfmBookCode
{
    public UsfmBookCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToUpperInvariant();
        if (!ValidCodeRegex().IsMatch(normalized))
        {
            throw new ArgumentException(
                "USFM book code must contain two or three letters, optionally prefixed by 1, 2, or 3.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[1-3]?[A-Z]{2,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidCodeRegex();
}
