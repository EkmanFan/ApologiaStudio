using ApologiaStudio.Application.BibleCorpora.Queries;

namespace ApologiaStudio.Application.BibleCorpora.Reader;

public enum BibleReaderStatus
{
    Ready = 0,
    CorpusUnavailable = 1,
    EditionNotFound = 2,
    BookNotFound = 3,
    ChapterNotFound = 4
}

public sealed record BibleReaderLocation(
    string EditionCode,
    string BookCode,
    int ChapterNumber);

public sealed class BibleReaderView
{
    public BibleReaderView(
        BibleReaderStatus status,
        IEnumerable<BibleEditionSummary> editions,
        BibleEditionSummary? edition = null,
        IEnumerable<BibleBookSummary>? books = null,
        BibleChapter? chapter = null,
        BibleReaderLocation? previousChapter = null,
        BibleReaderLocation? nextChapter = null)
    {
        ArgumentNullException.ThrowIfNull(editions);

        Status = status;
        Editions = editions.ToArray();
        Edition = edition;
        Books = books?.ToArray() ?? [];
        Chapter = chapter;
        PreviousChapter = previousChapter;
        NextChapter = nextChapter;

        if (status == BibleReaderStatus.Ready &&
            (edition is null || chapter is null))
        {
            throw new ArgumentException(
                "A ready Bible reader requires an edition and a chapter.",
                nameof(status));
        }
    }

    public BibleReaderStatus Status { get; }

    public IReadOnlyList<BibleEditionSummary> Editions { get; }

    public BibleEditionSummary? Edition { get; }

    public IReadOnlyList<BibleBookSummary> Books { get; }

    public BibleChapter? Chapter { get; }

    public BibleReaderLocation? PreviousChapter { get; }

    public BibleReaderLocation? NextChapter { get; }
}
