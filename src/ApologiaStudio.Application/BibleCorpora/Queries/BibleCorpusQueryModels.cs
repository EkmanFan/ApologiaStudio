namespace ApologiaStudio.Application.BibleCorpora.Queries;

public sealed record BibleEditionSummary(
    string Code,
    string DisplayName,
    string LanguageTag,
    string CanonCode);

public sealed record BibleBookSummary(
    string Code,
    string OsisCode,
    int CanonicalOrder,
    string DisplayName,
    string? ShortName,
    int ChapterCount);

public sealed class BibleEditionBooks
{
    public BibleEditionBooks(
        BibleEditionSummary edition,
        IEnumerable<BibleBookSummary> books)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(books);

        Edition = edition;
        Books = books.ToArray();
    }

    public BibleEditionSummary Edition { get; }

    public IReadOnlyList<BibleBookSummary> Books { get; }
}

public sealed record BibleWordAnnotation(
    int SourceOrdinal,
    string Marker,
    string Name,
    string Value,
    int CharacterOffset,
    int CharacterLength);

public sealed class BibleVerseText
{
    public BibleVerseText(
        string bookCode,
        int chapterNumber,
        string verseLabel,
        int verseOrdinal,
        string text,
        IEnumerable<BibleWordAnnotation> wordAnnotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookCode);
        ArgumentOutOfRangeException.ThrowIfLessThan(chapterNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(verseLabel);
        ArgumentOutOfRangeException.ThrowIfLessThan(verseOrdinal, 1);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(wordAnnotations);

        BookCode = bookCode;
        ChapterNumber = chapterNumber;
        VerseLabel = verseLabel;
        VerseOrdinal = verseOrdinal;
        Text = text;
        WordAnnotations = wordAnnotations.ToArray();
    }

    public string BookCode { get; }

    public int ChapterNumber { get; }

    public string VerseLabel { get; }

    public int VerseOrdinal { get; }

    public string Text { get; }

    public IReadOnlyList<BibleWordAnnotation> WordAnnotations { get; }
}

public sealed class BibleChapter
{
    public BibleChapter(
        BibleEditionSummary edition,
        BibleBookSummary book,
        int chapterNumber,
        IEnumerable<BibleVerseText> verses)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentOutOfRangeException.ThrowIfLessThan(chapterNumber, 1);
        ArgumentNullException.ThrowIfNull(verses);

        Edition = edition;
        Book = book;
        ChapterNumber = chapterNumber;
        Verses = verses.ToArray();
    }

    public BibleEditionSummary Edition { get; }

    public BibleBookSummary Book { get; }

    public int ChapterNumber { get; }

    public IReadOnlyList<BibleVerseText> Verses { get; }
}
