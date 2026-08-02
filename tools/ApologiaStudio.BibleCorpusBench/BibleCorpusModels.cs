namespace ApologiaStudio.BibleCorpusBench;

public readonly record struct VerseKey
{
    public VerseKey(string bookCode, int chapter, string verse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookCode);
        ArgumentOutOfRangeException.ThrowIfLessThan(chapter, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(verse);

        BookCode = bookCode.Trim().ToUpperInvariant();
        Chapter = chapter;
        Verse = verse.Trim().ToLowerInvariant();
    }

    public string BookCode { get; }

    public int Chapter { get; }

    public string Verse { get; }

    public override string ToString() => $"{BookCode} {Chapter}:{Verse}";
}

public sealed record ParsedWordAnnotation(
    string Marker,
    string Name,
    string Value,
    int CharacterOffset,
    int CharacterLength);

public sealed record ParsedSupplementalText(
    string Marker,
    string Text,
    int CharacterOffset,
    bool OccurredWithinVerse);

public sealed record BibleVerse(
    VerseKey Key,
    string Text,
    string Source,
    int SourceLine,
    IReadOnlyList<ParsedWordAnnotation> WordAnnotations,
    IReadOnlyList<ParsedSupplementalText> SupplementalTexts);

public sealed record CorpusReadResult(
    IReadOnlyDictionary<VerseKey, BibleVerse> Verses,
    int FileCount,
    int BookCount,
    int StrongAttributeCount);

public sealed record ReferenceDifference(
    string Reference,
    string? UsfmText,
    string? VplText);

public sealed record CorpusValidationReport(
    string CorpusName,
    DateTimeOffset GeneratedAtUtc,
    int ExpectedBookCount,
    bool StrongAttributesRequired,
    int UsfmFileCount,
    int UsfmBookCount,
    int UsfmVerseCount,
    int VplFileCount,
    int VplBookCount,
    int VplVerseCount,
    int StrongAttributeCount,
    int MissingFromUsfmCount,
    int UnexpectedInUsfmCount,
    int TextMismatchCount,
    IReadOnlyList<ReferenceDifference> Differences,
    bool IsMatch);

public sealed class BibleCorpusException : Exception
{
    public BibleCorpusException(string message)
        : base(message)
    {
    }

    public BibleCorpusException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
