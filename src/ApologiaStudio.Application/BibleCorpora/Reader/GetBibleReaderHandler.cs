using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Reader;

public sealed class GetBibleReaderHandler(
    IBibleCorpusQueryRepository repository)
{
    public async Task<BibleReaderView> HandleAsync(
        GetBibleReaderQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var editions = await repository.ListActiveEditionsAsync(
            cancellationToken);

        if (editions.Count == 0)
        {
            return new BibleReaderView(
                BibleReaderStatus.CorpusUnavailable,
                editions);
        }

        var edition = editions.SingleOrDefault(
            candidate => candidate.Code.Equals(
                query.EditionCode,
                StringComparison.OrdinalIgnoreCase));

        if (edition is null)
        {
            return new BibleReaderView(
                BibleReaderStatus.EditionNotFound,
                editions);
        }

        BibleEditionBooks? editionBooks;

        try
        {
            editionBooks = await repository.GetBooksAsync(
                new BibleEditionCode(edition.Code),
                cancellationToken);
        }
        catch (ArgumentException)
        {
            editionBooks = null;
        }

        if (editionBooks is null || editionBooks.Books.Count == 0)
        {
            return new BibleReaderView(
                BibleReaderStatus.CorpusUnavailable,
                editions,
                edition);
        }

        var books = editionBooks.Books;
        var requestedBookCode = string.IsNullOrWhiteSpace(query.BookCode)
            ? books[0].Code
            : query.BookCode;

        var bookIndex = FindBookIndex(
            books,
            requestedBookCode);

        if (bookIndex < 0)
        {
            return new BibleReaderView(
                BibleReaderStatus.BookNotFound,
                editions,
                edition,
                books);
        }

        var book = books[bookIndex];
        var chapterNumber = query.ChapterNumber ?? 1;

        if (chapterNumber < 1 || chapterNumber > book.ChapterCount)
        {
            return new BibleReaderView(
                BibleReaderStatus.ChapterNotFound,
                editions,
                edition,
                books);
        }

        BibleChapter? chapter;

        try
        {
            chapter = await repository.GetChapterAsync(
                new BibleEditionCode(edition.Code),
                new UsfmBookCode(book.Code),
                chapterNumber,
                cancellationToken);
        }
        catch (ArgumentException)
        {
            chapter = null;
        }

        if (chapter is null)
        {
            return new BibleReaderView(
                BibleReaderStatus.ChapterNotFound,
                editions,
                edition,
                books);
        }

        return new BibleReaderView(
            BibleReaderStatus.Ready,
            editions,
            edition,
            books,
            chapter,
            GetPreviousLocation(
                edition.Code,
                books,
                bookIndex,
                chapterNumber),
            GetNextLocation(
                edition.Code,
                books,
                bookIndex,
                chapterNumber));
    }

    private static int FindBookIndex(
        IReadOnlyList<BibleBookSummary> books,
        string? bookCode)
    {
        if (string.IsNullOrWhiteSpace(bookCode))
        {
            return -1;
        }

        for (var index = 0; index < books.Count; index++)
        {
            if (books[index].Code.Equals(
                    bookCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static BibleReaderLocation? GetPreviousLocation(
        string editionCode,
        IReadOnlyList<BibleBookSummary> books,
        int bookIndex,
        int chapterNumber)
    {
        if (chapterNumber > 1)
        {
            return new BibleReaderLocation(
                editionCode,
                books[bookIndex].Code,
                chapterNumber - 1);
        }

        if (bookIndex == 0)
        {
            return null;
        }

        var previousBook = books[bookIndex - 1];

        return new BibleReaderLocation(
            editionCode,
            previousBook.Code,
            previousBook.ChapterCount);
    }

    private static BibleReaderLocation? GetNextLocation(
        string editionCode,
        IReadOnlyList<BibleBookSummary> books,
        int bookIndex,
        int chapterNumber)
    {
        if (chapterNumber < books[bookIndex].ChapterCount)
        {
            return new BibleReaderLocation(
                editionCode,
                books[bookIndex].Code,
                chapterNumber + 1);
        }

        if (bookIndex == books.Count - 1)
        {
            return null;
        }

        return new BibleReaderLocation(
            editionCode,
            books[bookIndex + 1].Code,
            1);
    }
}
