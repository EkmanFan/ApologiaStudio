using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.Abstractions.BibleCorpora;

public interface IBibleCorpusQueryRepository
{
    Task<IReadOnlyList<BibleEditionSummary>> ListActiveEditionsAsync(
        CancellationToken cancellationToken);

    Task<BibleEditionBooks?> GetBooksAsync(
        BibleEditionCode editionCode,
        CancellationToken cancellationToken);

    Task<BibleChapter?> GetChapterAsync(
        BibleEditionCode editionCode,
        UsfmBookCode bookCode,
        int chapterNumber,
        CancellationToken cancellationToken);

    Task<BibleVerseText?> GetVerseAsync(
        BibleEditionCode editionCode,
        BibleReference reference,
        CancellationToken cancellationToken);
}
