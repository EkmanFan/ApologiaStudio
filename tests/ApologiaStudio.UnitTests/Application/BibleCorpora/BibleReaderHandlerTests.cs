using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Application.BibleCorpora.Reader;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.BibleCorpora;

public sealed class BibleReaderHandlerTests
{
    [Fact]
    public async Task Reader_ShouldNavigateAcrossBookBoundaries()
    {
        var repository = CreateRepository();
        var handler = new GetBibleReaderHandler(repository);

        var first = await handler.HandleAsync(
            new GetBibleReaderQuery("LSG1910", "GEN", 1),
            CancellationToken.None);

        Assert.Equal(BibleReaderStatus.Ready, first.Status);
        Assert.Null(first.PreviousChapter);
        Assert.Equal(
            new BibleReaderLocation("lsg1910", "GEN", 2),
            first.NextChapter);

        var lastGenesisChapter = await handler.HandleAsync(
            new GetBibleReaderQuery("lsg1910", "GEN", 2),
            CancellationToken.None);

        Assert.Equal(
            new BibleReaderLocation("lsg1910", "GEN", 1),
            lastGenesisChapter.PreviousChapter);

        Assert.Equal(
            new BibleReaderLocation("lsg1910", "EXO", 1),
            lastGenesisChapter.NextChapter);

        var exodus = await handler.HandleAsync(
            new GetBibleReaderQuery("lsg1910", "EXO", 1),
            CancellationToken.None);

        Assert.Equal(
            new BibleReaderLocation("lsg1910", "GEN", 2),
            exodus.PreviousChapter);

        Assert.Null(exodus.NextChapter);
    }

    [Fact]
    public async Task Reader_ShouldOpenFirstBookWhenOnlyEditionIsSpecified()
    {
        var handler = new GetBibleReaderHandler(
            CreateRepository());

        var result = await handler.HandleAsync(
            new GetBibleReaderQuery("lsg1910"),
            CancellationToken.None);

        Assert.Equal(BibleReaderStatus.Ready, result.Status);
        Assert.Equal("GEN", result.Chapter!.Book.Code);
        Assert.Equal(1, result.Chapter.ChapterNumber);
    }

    [Fact]
    public async Task Reader_ShouldReturnExplicitMissingContentStates()
    {
        var repository = CreateRepository();
        var handler = new GetBibleReaderHandler(repository);

        var unknownEdition = await handler.HandleAsync(
            new GetBibleReaderQuery("unknown"),
            CancellationToken.None);

        var unknownBook = await handler.HandleAsync(
            new GetBibleReaderQuery("lsg1910", "JHN", 1),
            CancellationToken.None);

        var unknownChapter = await handler.HandleAsync(
            new GetBibleReaderQuery("lsg1910", "GEN", 99),
            CancellationToken.None);

        repository.Editions = [];

        var unavailable = await handler.HandleAsync(
            new GetBibleReaderQuery("lsg1910"),
            CancellationToken.None);

        Assert.Equal(
            BibleReaderStatus.EditionNotFound,
            unknownEdition.Status);

        Assert.Equal(
            BibleReaderStatus.BookNotFound,
            unknownBook.Status);

        Assert.Equal(
            BibleReaderStatus.ChapterNotFound,
            unknownChapter.Status);

        Assert.Equal(
            BibleReaderStatus.CorpusUnavailable,
            unavailable.Status);
    }

    [Fact]
    public async Task DiscussionDraft_ShouldRevalidateAndNormalizeTheReference()
    {
        var handler = new PrepareBibleDiscussionDraftHandler(
            CreateRepository());

        var draft = await handler.HandleAsync(
            new PrepareBibleDiscussionDraftQuery(
                "lsg1910",
                "GEN",
                1,
                "2",
                "1",
                ApplicationLanguage.French),
            CancellationToken.None);

        Assert.NotNull(draft);
        Assert.Equal(
            "Genèse 1:1-2",
            draft.NormalizedReference);

        Assert.Equal(
            "Analyse Genèse 1:1-2 dans Louis Segond 1910.",
            draft.Prompt);
    }

    [Fact]
    public async Task DiscussionDraft_ShouldRejectAClientOnlyVerseLabel()
    {
        var handler = new PrepareBibleDiscussionDraftHandler(
            CreateRepository());

        var result = await handler.HandleAsync(
            new PrepareBibleDiscussionDraftQuery(
                "lsg1910",
                "GEN",
                1,
                "1",
                "999",
                ApplicationLanguage.English),
            CancellationToken.None);

        Assert.Null(result);
    }

    private static FakeBibleCorpusQueryRepository CreateRepository()
    {
        var edition = new BibleEditionSummary(
            "lsg1910",
            "Louis Segond 1910",
            "fr",
            "protestant-66");

        var genesis = new BibleBookSummary(
            "GEN",
            "Gen",
            1,
            "Genèse",
            "Gen",
            2);

        var exodus = new BibleBookSummary(
            "EXO",
            "Exod",
            2,
            "Exode",
            "Ex",
            1);

        return new FakeBibleCorpusQueryRepository(
            [edition],
            new BibleEditionBooks(
                edition,
                [genesis, exodus]),
            new Dictionary<(string BookCode, int ChapterNumber), BibleChapter>
            {
                [("GEN", 1)] = new BibleChapter(
                    edition,
                    genesis,
                    1,
                    [
                        new BibleVerseText(
                            "GEN",
                            1,
                            "1",
                            1,
                            "Au commencement",
                            []),
                        new BibleVerseText(
                            "GEN",
                            1,
                            "2",
                            2,
                            "La terre était informe",
                            [])
                    ]),
                [("GEN", 2)] = new BibleChapter(
                    edition,
                    genesis,
                    2,
                    [
                        new BibleVerseText(
                            "GEN",
                            2,
                            "1",
                            1,
                            "Ainsi furent achevés les cieux",
                            [])
                    ]),
                [("EXO", 1)] = new BibleChapter(
                    edition,
                    exodus,
                    1,
                    [
                        new BibleVerseText(
                            "EXO",
                            1,
                            "1",
                            1,
                            "Voici les noms",
                            [])
                    ])
            });
    }

    private sealed class FakeBibleCorpusQueryRepository(
        IReadOnlyList<BibleEditionSummary> editions,
        BibleEditionBooks editionBooks,
        IReadOnlyDictionary<(string BookCode, int ChapterNumber), BibleChapter>
            chapters)
        : IBibleCorpusQueryRepository
    {
        public IReadOnlyList<BibleEditionSummary> Editions { get; set; } =
            editions;

        public Task<IReadOnlyList<BibleEditionSummary>> ListActiveEditionsAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Editions);
        }

        public Task<BibleEditionBooks?> GetBooksAsync(
            BibleEditionCode editionCode,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<BibleEditionBooks?>(
                editionCode.Value == editionBooks.Edition.Code
                    ? editionBooks
                    : null);
        }

        public Task<BibleChapter?> GetChapterAsync(
            BibleEditionCode editionCode,
            UsfmBookCode bookCode,
            int chapterNumber,
            CancellationToken cancellationToken)
        {
            chapters.TryGetValue(
                (bookCode.Value, chapterNumber),
                out var chapter);

            return Task.FromResult<BibleChapter?>(chapter);
        }

        public Task<BibleVerseText?> GetVerseAsync(
            BibleEditionCode editionCode,
            BibleReference reference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<BibleVerseText?>(null);
        }
    }
}
