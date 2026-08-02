using System.Text.RegularExpressions;

namespace ApologiaStudio.Domain.BibleCorpora;

public readonly partial record struct BibleReference
{
    public BibleReference(
        UsfmBookCode bookCode,
        int chapterNumber,
        string verseLabel)
    {
        if (string.IsNullOrWhiteSpace(bookCode.Value))
        {
            throw new ArgumentException(
                "USFM book code cannot be empty.",
                nameof(bookCode));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(chapterNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(verseLabel);

        var normalizedLabel = verseLabel.Trim().ToLowerInvariant();
        if (!ValidVerseLabelRegex().IsMatch(normalizedLabel))
        {
            throw new ArgumentException(
                "Verse label is not a supported USFM verse number, segment, range, or list.",
                nameof(verseLabel));
        }

        BookCode = bookCode;
        ChapterNumber = chapterNumber;
        VerseLabel = normalizedLabel;
    }

    public UsfmBookCode BookCode { get; }

    public int ChapterNumber { get; }

    public string VerseLabel { get; }

    public override string ToString() => $"{BookCode} {ChapterNumber}:{VerseLabel}";

    [GeneratedRegex("^[0-9]+[a-z]?(?:[-,][0-9]+[a-z]?)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidVerseLabelRegex();
}
