namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed class BibleCorpusReadResult
{
    public BibleCorpusReadResult(
        int sourceFileCount,
        IEnumerable<ParsedBibleBook> books,
        IEnumerable<ParsedBibleVerse> verses)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceFileCount, 1);
        ArgumentNullException.ThrowIfNull(books);
        ArgumentNullException.ThrowIfNull(verses);

        SourceFileCount = sourceFileCount;
        Books = books.ToArray();
        Verses = verses.ToArray();
    }

    public int SourceFileCount { get; }

    public IReadOnlyList<ParsedBibleBook> Books { get; }

    public IReadOnlyList<ParsedBibleVerse> Verses { get; }
}
