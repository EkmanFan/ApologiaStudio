using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.BibleCorpora.Reader;

public sealed class PrepareBibleDiscussionDraftHandler(
    IBibleCorpusQueryRepository repository)
{
    public async Task<BibleDiscussionDraft?> HandleAsync(
        PrepareBibleDiscussionDraftQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        query.Language.EnsureSupported(
            nameof(query.Language));

        if (query.ChapterNumber < 1 ||
            string.IsNullOrWhiteSpace(query.StartVerseLabel))
        {
            return null;
        }

        BibleEditionCode editionCode;
        UsfmBookCode bookCode;

        try
        {
            editionCode = new BibleEditionCode(query.EditionCode);
            bookCode = new UsfmBookCode(query.BookCode);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var editionBooks = await repository.GetBooksAsync(
            editionCode,
            cancellationToken);

        var book = editionBooks?.Books.SingleOrDefault(
            candidate => candidate.Code.Equals(
                bookCode.Value,
                StringComparison.OrdinalIgnoreCase));

        if (editionBooks is null || book is null)
        {
            return null;
        }

        var chapter = await repository.GetChapterAsync(
            editionCode,
            bookCode,
            query.ChapterNumber,
            cancellationToken);

        if (chapter is null)
        {
            return null;
        }

        var startVerse = chapter.Verses.SingleOrDefault(
            verse => verse.VerseLabel.Equals(
                query.StartVerseLabel,
                StringComparison.Ordinal));

        var requestedEndLabel = string.IsNullOrWhiteSpace(
            query.EndVerseLabel)
            ? query.StartVerseLabel
            : query.EndVerseLabel;

        var endVerse = chapter.Verses.SingleOrDefault(
            verse => verse.VerseLabel.Equals(
                requestedEndLabel,
                StringComparison.Ordinal));

        if (startVerse is null || endVerse is null)
        {
            return null;
        }

        if (startVerse.VerseOrdinal > endVerse.VerseOrdinal)
        {
            (startVerse, endVerse) = (endVerse, startVerse);
        }

        var normalizedReference =
            $"{book.DisplayName} {query.ChapterNumber}:" +
            startVerse.VerseLabel +
            (startVerse.VerseOrdinal == endVerse.VerseOrdinal
                ? string.Empty
                : $"-{endVerse.VerseLabel}");

        var prompt = query.Language == ApplicationLanguage.French
            ? $"Analyse {normalizedReference} dans {editionBooks.Edition.DisplayName}."
            : $"Analyze {normalizedReference} in {editionBooks.Edition.DisplayName}.";

        return new BibleDiscussionDraft(
            prompt,
            normalizedReference);
    }
}
