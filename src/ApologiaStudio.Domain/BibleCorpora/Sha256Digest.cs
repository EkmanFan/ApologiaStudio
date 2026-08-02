using System.Text.RegularExpressions;

namespace ApologiaStudio.Domain.BibleCorpora;

public readonly partial record struct Sha256Digest
{
    public Sha256Digest(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToLowerInvariant();
        if (!ValidDigestRegex().IsMatch(normalized))
        {
            throw new ArgumentException(
                "SHA-256 digest must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidDigestRegex();
}
