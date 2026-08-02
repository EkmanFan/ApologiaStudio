using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record ParsedBibleVerse
{
    public ParsedBibleVerse(
        BibleReference reference,
        int verseOrdinal,
        string text,
        string sourceRelativePath,
        int sourceLine,
        IEnumerable<ParsedBibleWordAnnotation>? wordAnnotations = null,
        IEnumerable<ParsedBibleSupplementalText>? supplementalTexts = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(verseOrdinal, 1);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelativePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceLine, 1);

        Reference = reference;
        VerseOrdinal = verseOrdinal;
        Text = text;
        SourceRelativePath = sourceRelativePath.Trim();
        SourceLine = sourceLine;
        WordAnnotations = wordAnnotations?.ToArray()
            ?? [];
        SupplementalTexts = supplementalTexts?.ToArray()
            ?? [];
    }

    public BibleReference Reference { get; }

    public int VerseOrdinal { get; }

    public string Text { get; }

    public string SourceRelativePath { get; }

    public int SourceLine { get; }

    public IReadOnlyList<ParsedBibleWordAnnotation> WordAnnotations { get; }

    public IReadOnlyList<ParsedBibleSupplementalText> SupplementalTexts { get; }
}
