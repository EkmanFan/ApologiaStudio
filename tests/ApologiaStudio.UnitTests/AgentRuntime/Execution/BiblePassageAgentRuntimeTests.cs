using System.Runtime.CompilerServices;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Execution;

public sealed class BiblePassageAgentRuntimeTests
{
    [Fact]
    public async Task RunTurnAsync_ShouldReturnFrenchVerseFromRepository()
    {
        var repository = new StubBibleRepository();
        var fallback = new TrackingFallbackRuntime();
        var runtime = CreateRuntime(repository, fallback);

        var events = await CollectEventsAsync(
            runtime,
            CreateRequest("Donne-moi Jean 3:16."));

        Assert.Equal(0, fallback.CallCount);
        Assert.Equal("lsg1910", repository.LastEditionCode?.Value);
        Assert.Equal("JHN 3:16", repository.LastReference?.ToString());

        Assert.Collection(
            events,
            selected =>
                Assert.Equal(
                    BuiltInAgents.ProtestantApologist.Id,
                    Assert.IsType<AgentSelectedEvent>(selected).AgentId),
            delta =>
                Assert.Equal(
                    "Jean 3:16 (Louis Segond 1910)\n\n" +
                    "Car Dieu a tant aimé le monde\n\n" +
                    "Source : corpus biblique · lsg1910 · PostgreSQL",
                    Assert.IsType<TextDeltaEvent>(delta).Content),
            completed =>
                Assert.DoesNotContain(
                    "G2316",
                    Assert.IsType<AgentTurnCompletedEvent>(completed).Content,
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunTurnAsync_ShouldReturnWholeChapterFromRepository()
    {
        var repository = new StubBibleRepository();
        var fallback = new TrackingFallbackRuntime();
        var runtime = CreateRuntime(repository, fallback);

        var events = await CollectEventsAsync(
            runtime,
            CreateRequest("Donne-moi 1 Corinthiens 13."));

        Assert.Equal(0, fallback.CallCount);
        Assert.Equal("lsg1910", repository.LastEditionCode?.Value);
        Assert.Equal("1CO", repository.LastBookCode?.Value);
        Assert.Equal(13, repository.LastChapterNumber);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Contains(
            "1. Quand je parlerais les langues des hommes",
            completed.Content,
            StringComparison.Ordinal);

        Assert.Contains(
            "13. Maintenant donc ces trois choses demeurent",
            completed.Content,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "14.",
            completed.Content,
            StringComparison.Ordinal);

        Assert.EndsWith(
            "Source : corpus biblique · lsg1910 · PostgreSQL",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldUseNormalizedMisspelledReference()
    {
        var repository = new StubBibleRepository();
        var fallback = new TrackingFallbackRuntime();
        var router = new StubAgentRouter(
            new RoutingDecision(
                BuiltInAgents.ProtestantApologist.Id,
                BuiltInAgents.ProtestantApologist.DisplayName,
                "La demande cite une référence biblique normalisée.",
                0.98,
                WasExplicitlyRequested: false,
                BiblePassageResolution.Resolved,
                new BiblePassageRequest(
                    null,
                    new UsfmBookCode("1CO"),
                    13,
                    VerseLabel: null)));

        var runtime = CreateRuntime(
            repository,
            fallback,
            router);

        var events = await CollectEventsAsync(
            runtime,
            CreateRequest("Donne-moi 1 Corinthien 13."));

        Assert.Equal(1, router.CallCount);
        Assert.Equal(0, fallback.CallCount);
        Assert.Equal("1CO", repository.LastBookCode?.Value);
        Assert.Equal(13, repository.LastChapterNumber);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.EndsWith(
            "Source : corpus biblique · lsg1910 · PostgreSQL",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldUsePreferenceInsteadOfBookNameLanguage()
    {
        var repository = new StubBibleRepository();
        var runtime = CreateRuntime(
            repository,
            new TrackingFallbackRuntime());

        await CollectEventsAsync(
            runtime,
            CreateRequest("Please read John 3:16."));

        Assert.Equal(
            "lsg1910",
            repository.LastEditionCode?.Value);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldUseEnglishTheologicalPreferenceByDefault()
    {
        var repository = new StubBibleRepository();
        var runtime = CreateRuntime(
            repository,
            new TrackingFallbackRuntime());

        await CollectEventsAsync(
            runtime,
            CreateRequest(
                "Jean 3:16",
                theologicalLanguage:
                    ApplicationLanguage.English));

        Assert.Equal(
            "web-classic",
            repository.LastEditionCode?.Value);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldPrioritizeExplicitMessageLanguage()
    {
        var repository = new StubBibleRepository();
        var runtime = CreateRuntime(
            repository,
            new TrackingFallbackRuntime());

        await CollectEventsAsync(
            runtime,
            CreateRequest(
                "Jean 3:16 en anglais",
                theologicalLanguage:
                    ApplicationLanguage.French));

        Assert.Equal(
            "web-classic",
            repository.LastEditionCode?.Value);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldReturnNotFoundWithoutCallingModel()
    {
        var repository = new StubBibleRepository(
            verseExists: false);

        var fallback = new TrackingFallbackRuntime();
        var runtime = CreateRuntime(repository, fallback);

        var events = await CollectEventsAsync(
            runtime,
            CreateRequest("Jean 999:999"));

        Assert.Equal(0, fallback.CallCount);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Contains(
            "n’a pas été trouvé",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldDelegateNonReferenceMessage()
    {
        var fallback = new TrackingFallbackRuntime();
        var runtime = CreateRuntime(
            new StubBibleRepository(),
            fallback);

        var events = await CollectEventsAsync(
            runtime,
            CreateRequest("Comment défendre la résurrection ?"));

        Assert.Equal(1, fallback.CallCount);
        Assert.Single(events);
        Assert.IsType<AgentTurnCompletedEvent>(events[0]);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldReturnBibleRangeFromRepository()
    {
        var fallback = new TrackingFallbackRuntime();
        var repository = new StubBibleRepository();
        var runtime = CreateRuntime(
            repository,
            fallback);

        var events = await CollectEventsAsync(
            runtime,
            CreateRequest("Donne-moi Jean 3:16-18."));

        Assert.Equal(0, fallback.CallCount);
        Assert.Equal("JHN", repository.LastBookCode?.Value);
        Assert.Equal(3, repository.LastChapterNumber);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Contains(
            "Jean 3:16-18",
            completed.Content,
            StringComparison.Ordinal);

        Assert.Contains(
            "16. Texte du verset 16",
            completed.Content,
            StringComparison.Ordinal);

        Assert.Contains(
            "18. Texte du verset 18",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldNotGenerateForInvalidNormalization()
    {
        var fallback = new TrackingFallbackRuntime();
        var router = new StubAgentRouter(
            new RoutingDecision(
                BuiltInAgents.ProtestantApologist.Id,
                BuiltInAgents.ProtestantApologist.DisplayName,
                "La référence n’a pas pu être normalisée.",
                0.50,
                WasExplicitlyRequested: false,
                BiblePassageResolution.Unsupported));

        var runtime = CreateRuntime(
            new StubBibleRepository(),
            fallback,
            router);

        var events = await CollectEventsAsync(
            runtime,
            CreateRequest("Donne-moi 9 Corinthien 999."));

        Assert.Equal(0, fallback.CallCount);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Contains(
            "pas pu normaliser",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldRespectExplicitHistorianSelection()
    {
        var fallback = new TrackingFallbackRuntime();
        var runtime = CreateRuntime(
            new StubBibleRepository(),
            fallback);

        await CollectEventsAsync(
            runtime,
            CreateRequest(
                "Jean 3:16",
                BuiltInAgents.Historian.Id));

        Assert.Equal(1, fallback.CallCount);
    }

    private static BiblePassageAgentRuntime CreateRuntime(
        IBibleCorpusQueryRepository repository,
        IAgentRuntime fallbackRuntime,
        IAgentRouter? router = null)
    {
        var parser = new BiblePassageRequestParser();

        return new BiblePassageAgentRuntime(
            router ?? new DeterministicAgentRouter(parser),
            repository,
            fallbackRuntime);
    }

    private static async Task<List<AgentRunEvent>> CollectEventsAsync(
        IAgentRuntime runtime,
        AgentTurnRequest request)
    {
        var events = new List<AgentRunEvent>();

        await foreach (var runEvent in runtime.RunTurnAsync(
                           request,
                           CancellationToken.None))
        {
            events.Add(runEvent);
        }

        return events;
    }

    private static AgentTurnRequest CreateRequest(
        string content,
        AgentId? requestedAgentId = null,
        ApplicationLanguage theologicalLanguage =
            ApplicationLanguage.French)
    {
        var messageId = MessageId.New();

        return new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            messageId,
            requestedAgentId,
            History:
            [
                new ConversationMessageContext(
                    messageId,
                    MessageRole.User,
                    content,
                    AgentId: null,
                    DateTimeOffset.UtcNow)
            ],
            theologicalLanguage);
    }

    private sealed class TrackingFallbackRuntime : IAgentRuntime
    {
        public int CallCount { get; private set; }

        public async IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
            AgentTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            CallCount++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return new AgentTurnCompletedEvent(
                BuiltInAgents.Historian.Id,
                "Fallback response");
        }
    }

    private sealed class StubBibleRepository(
        bool verseExists = true)
        : IBibleCorpusQueryRepository
    {
        public BibleEditionCode? LastEditionCode { get; private set; }

        public BibleReference? LastReference { get; private set; }

        public UsfmBookCode? LastBookCode { get; private set; }

        public int? LastChapterNumber { get; private set; }

        public Task<IReadOnlyList<BibleEditionSummary>>
            ListActiveEditionsAsync(
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BibleEditionBooks?> GetBooksAsync(
            BibleEditionCode editionCode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastEditionCode = editionCode;

            var isEnglish =
                editionCode.Value == "web-classic";

            BibleEditionBooks result = new(
                new BibleEditionSummary(
                    editionCode.Value,
                    isEnglish
                        ? "World English Bible Classic"
                        : "Louis Segond 1910",
                    isEnglish ? "en" : "fr",
                    "protestant-66"),
                [
                    new BibleBookSummary(
                        "JHN",
                        "John",
                        43,
                        isEnglish ? "John" : "Jean",
                        isEnglish ? "John" : "Jn",
                        21)
                ]);

            return Task.FromResult<BibleEditionBooks?>(result);
        }

        public Task<BibleChapter?> GetChapterAsync(
            BibleEditionCode editionCode,
            UsfmBookCode bookCode,
            int chapterNumber,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastEditionCode = editionCode;
            LastBookCode = bookCode;
            LastChapterNumber = chapterNumber;

            var edition = new BibleEditionSummary(
                editionCode.Value,
                "Louis Segond 1910",
                "fr",
                "protestant-66");

            var isJohn = bookCode.Value == "JHN";

            var book = new BibleBookSummary(
                bookCode.Value,
                isJohn ? "John" : "1Cor",
                isJohn ? 43 : 46,
                isJohn
                    ? "Jean"
                    : "Première épître de Paul aux Corinthiens",
                isJohn ? "Jean" : "1 Corinthiens",
                isJohn ? 21 : 16);

            var verseCount = isJohn ? 21 : 13;

            BibleChapter result = new(
                edition,
                book,
                chapterNumber,
                Enumerable.Range(1, verseCount).Select(
                    verseNumber => new BibleVerseText(
                        bookCode.Value,
                        chapterNumber,
                        verseNumber.ToString(),
                        verseNumber,
                        verseNumber switch
                        {
                            1 => "Quand je parlerais les langues des hommes",
                            13 => "Maintenant donc ces trois choses demeurent",
                            _ => $"Texte du verset {verseNumber}"
                        },
                        [])));

            return Task.FromResult<BibleChapter?>(result);
        }

        public Task<BibleVerseText?> GetVerseAsync(
            BibleEditionCode editionCode,
            BibleReference reference,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastEditionCode = editionCode;
            LastReference = reference;

            if (!verseExists)
            {
                return Task.FromResult<BibleVerseText?>(null);
            }

            var text = editionCode.Value == "web-classic"
                ? "For God so loved the world"
                : "Car Dieu a tant aimé le monde";

            BibleVerseText result = new(
                reference.BookCode.Value,
                reference.ChapterNumber,
                reference.VerseLabel,
                16,
                text,
                [
                    new BibleWordAnnotation(
                        1,
                        "w",
                        "strong",
                        "G2316",
                        4,
                        4)
                ]);

            return Task.FromResult<BibleVerseText?>(result);
        }
    }

    private sealed class StubAgentRouter(
        RoutingDecision decision)
        : IAgentRouter
    {
        public int CallCount { get; private set; }

        public ValueTask<RoutingDecision> RouteAsync(
            AgentTurnRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            return ValueTask.FromResult(decision);
        }
    }
}
