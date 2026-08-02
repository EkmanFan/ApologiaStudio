using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Repositories;

public sealed class EfBibleCorpusQueryRepository(
    ApologiaStudioDbContext dbContext)
    : IBibleCorpusQueryRepository
{
    public async Task<IReadOnlyList<BibleEditionSummary>> ListActiveEditionsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await (
                from version in dbContext.Set<BibleCorpusVersionEntity>()
                join edition in dbContext.Set<BibleEditionEntity>()
                    on version.EditionCode equals edition.Code
                where version.IsActive
                      && version.ValidationStatus == "approved"
                orderby edition.LanguageTag, edition.Code
                select new
                {
                    edition.Code,
                    edition.DisplayName,
                    edition.LanguageTag,
                    edition.CanonCode
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new BibleEditionSummary(
                row.Code.Value,
                row.DisplayName,
                row.LanguageTag,
                row.CanonCode))
            .ToArray();
    }

    public async Task<BibleEditionBooks?> GetBooksAsync(
        BibleEditionCode editionCode,
        CancellationToken cancellationToken)
    {
        var edition = await GetActiveEditionAsync(
            editionCode,
            cancellationToken);

        if (edition is null)
        {
            return null;
        }

        var rows = await (
                from corpusBook in dbContext.Set<BibleCorpusBookEntity>()
                join book in dbContext.Set<BibleBookEntity>()
                    on corpusBook.UsfmBookCode equals book.UsfmCode
                where corpusBook.CorpusVersionId == edition.CorpusVersionId
                orderby corpusBook.BookOrdinal
                select new
                {
                    corpusBook.UsfmBookCode,
                    book.OsisCode,
                    book.CanonicalOrder,
                    corpusBook.DisplayName,
                    corpusBook.ShortName,
                    ChapterCount = dbContext.Set<BibleVerseEntity>()
                        .Where(verse =>
                            verse.CorpusVersionId == edition.CorpusVersionId
                            && verse.UsfmBookCode == corpusBook.UsfmBookCode)
                        .Select(verse => verse.ChapterNumber)
                        .Distinct()
                        .Count()
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new BibleEditionBooks(
            edition.ToSummary(),
            rows.Select(row => new BibleBookSummary(
                row.UsfmBookCode.Value,
                row.OsisCode,
                row.CanonicalOrder,
                row.DisplayName,
                row.ShortName,
                row.ChapterCount)));
    }

    public async Task<BibleChapter?> GetChapterAsync(
        BibleEditionCode editionCode,
        UsfmBookCode bookCode,
        int chapterNumber,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chapterNumber, 1);

        var book = await GetActiveBookAsync(
            editionCode,
            bookCode,
            cancellationToken);

        if (book is null)
        {
            return null;
        }

        var verses = await dbContext.Set<BibleVerseEntity>()
            .AsNoTracking()
            .Where(verse =>
                verse.CorpusVersionId == book.Edition.CorpusVersionId
                && verse.UsfmBookCode == book.BookCode
                && verse.ChapterNumber == chapterNumber)
            .OrderBy(verse => verse.VerseOrdinal)
            .Select(verse => new VerseRow(
                verse.Id,
                verse.UsfmBookCode,
                verse.ChapterNumber,
                verse.VerseLabel,
                verse.VerseOrdinal,
                verse.Text))
            .ToListAsync(cancellationToken);

        if (verses.Count == 0)
        {
            return null;
        }

        var results = await AddAnnotationsAsync(
            verses,
            cancellationToken);

        return new BibleChapter(
            book.Edition.ToSummary(),
            book.ToSummary(),
            chapterNumber,
            results);
    }

    public async Task<BibleVerseText?> GetVerseAsync(
        BibleEditionCode editionCode,
        BibleReference reference,
        CancellationToken cancellationToken)
    {
        var edition = await GetActiveEditionAsync(
            editionCode,
            cancellationToken);

        if (edition is null)
        {
            return null;
        }

        var verse = await dbContext.Set<BibleVerseEntity>()
            .AsNoTracking()
            .Where(candidate =>
                candidate.CorpusVersionId == edition.CorpusVersionId
                && candidate.UsfmBookCode == reference.BookCode
                && candidate.ChapterNumber == reference.ChapterNumber
                && candidate.VerseLabel == reference.VerseLabel)
            .Select(candidate => new VerseRow(
                candidate.Id,
                candidate.UsfmBookCode,
                candidate.ChapterNumber,
                candidate.VerseLabel,
                candidate.VerseOrdinal,
                candidate.Text))
            .SingleOrDefaultAsync(cancellationToken);

        if (verse is null)
        {
            return null;
        }

        var result = await AddAnnotationsAsync(
            [verse],
            cancellationToken);

        return result[0];
    }

    private async Task<ActiveEditionRow?> GetActiveEditionAsync(
        BibleEditionCode editionCode,
        CancellationToken cancellationToken)
    {
        return await (
                from version in dbContext.Set<BibleCorpusVersionEntity>()
                join edition in dbContext.Set<BibleEditionEntity>()
                    on version.EditionCode equals edition.Code
                where version.EditionCode == editionCode
                      && version.IsActive
                      && version.ValidationStatus == "approved"
                select new ActiveEditionRow(
                    version.Id,
                    edition.Code,
                    edition.DisplayName,
                    edition.LanguageTag,
                    edition.CanonCode))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ActiveBookRow?> GetActiveBookAsync(
        BibleEditionCode editionCode,
        UsfmBookCode bookCode,
        CancellationToken cancellationToken)
    {
        return await (
                from version in dbContext.Set<BibleCorpusVersionEntity>()
                join edition in dbContext.Set<BibleEditionEntity>()
                    on version.EditionCode equals edition.Code
                join corpusBook in dbContext.Set<BibleCorpusBookEntity>()
                    on version.Id equals corpusBook.CorpusVersionId
                join book in dbContext.Set<BibleBookEntity>()
                    on corpusBook.UsfmBookCode equals book.UsfmCode
                where version.EditionCode == editionCode
                      && version.IsActive
                      && version.ValidationStatus == "approved"
                      && corpusBook.UsfmBookCode == bookCode
                select new ActiveBookRow(
                    new ActiveEditionRow(
                        version.Id,
                        edition.Code,
                        edition.DisplayName,
                        edition.LanguageTag,
                        edition.CanonCode),
                    corpusBook.UsfmBookCode,
                    book.OsisCode,
                    book.CanonicalOrder,
                    corpusBook.DisplayName,
                    corpusBook.ShortName,
                    dbContext.Set<BibleVerseEntity>()
                        .Where(verse =>
                            verse.CorpusVersionId == version.Id
                            && verse.UsfmBookCode == corpusBook.UsfmBookCode)
                        .Select(verse => verse.ChapterNumber)
                        .Distinct()
                        .Count()))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<BibleVerseText>> AddAnnotationsAsync(
        IReadOnlyList<VerseRow> verses,
        CancellationToken cancellationToken)
    {
        var verseIds = verses
            .Select(verse => verse.Id)
            .ToArray();

        var annotations = await dbContext.Set<BibleWordAnnotationEntity>()
            .AsNoTracking()
            .Where(annotation => verseIds.Contains(annotation.VerseId))
            .OrderBy(annotation => annotation.VerseId)
            .ThenBy(annotation => annotation.SourceOrdinal)
            .Select(annotation => new AnnotationRow(
                annotation.VerseId,
                annotation.SourceOrdinal,
                annotation.Marker,
                annotation.AttributeName,
                annotation.AttributeValue,
                annotation.CharacterOffset,
                annotation.CharacterLength))
            .ToListAsync(cancellationToken);

        var annotationsByVerse = annotations
            .GroupBy(annotation => annotation.VerseId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(annotation => new BibleWordAnnotation(
                        annotation.SourceOrdinal,
                        annotation.Marker,
                        annotation.Name,
                        annotation.Value,
                        annotation.CharacterOffset,
                        annotation.CharacterLength))
                    .ToArray());

        return verses
            .Select(verse => new BibleVerseText(
                verse.BookCode.Value,
                verse.ChapterNumber,
                verse.VerseLabel,
                verse.VerseOrdinal,
                verse.Text,
                annotationsByVerse.GetValueOrDefault(verse.Id)
                    ?? []))
            .ToArray();
    }

    private sealed record ActiveEditionRow(
        BibleCorpusVersionId CorpusVersionId,
        BibleEditionCode Code,
        string DisplayName,
        string LanguageTag,
        string CanonCode)
    {
        public BibleEditionSummary ToSummary() =>
            new(
                Code.Value,
                DisplayName,
                LanguageTag,
                CanonCode);
    }

    private sealed record ActiveBookRow(
        ActiveEditionRow Edition,
        UsfmBookCode BookCode,
        string OsisCode,
        int CanonicalOrder,
        string DisplayName,
        string? ShortName,
        int ChapterCount)
    {
        public BibleBookSummary ToSummary() =>
            new(
                BookCode.Value,
                OsisCode,
                CanonicalOrder,
                DisplayName,
                ShortName,
                ChapterCount);
    }

    private sealed record VerseRow(
        long Id,
        UsfmBookCode BookCode,
        int ChapterNumber,
        string VerseLabel,
        int VerseOrdinal,
        string Text);

    private sealed record AnnotationRow(
        long VerseId,
        int SourceOrdinal,
        string Marker,
        string Name,
        string Value,
        int CharacterOffset,
        int CharacterLength);
}
