using System.Runtime.CompilerServices;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class BiblePassageAgentRuntime(
    IAgentRouter agentRouter,
    IBibleCorpusQueryRepository bibleRepository,
    IAgentRuntime fallbackRuntime)
    : IAgentRuntime
{
    public async IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
        AgentTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var routingDecision = await agentRouter.RouteAsync(
            request,
            cancellationToken);

        if (routingDecision.AgentId !=
                BuiltInAgents.ProtestantApologist.Id ||
            routingDecision.BiblePassageResolution ==
                BiblePassageResolution.None)
        {
            await foreach (var runEvent in RunFallbackAsync(
                               request,
                               routingDecision,
                               cancellationToken)
                               .WithCancellation(cancellationToken))
            {
                yield return runEvent;
            }

            yield break;
        }

        if (routingDecision.BiblePassageResolution ==
                BiblePassageResolution.Unsupported ||
            routingDecision.BiblePassage is null)
        {
            await foreach (var runEvent in
                           CreateUnsupportedReferenceEventsAsync(
                               routingDecision,
                               request.TheologicalLanguage,
                               cancellationToken))
            {
                yield return runEvent;
            }

            yield break;
        }

        yield return new AgentSelectedEvent(
            routingDecision.AgentId,
            routingDecision.AgentName,
            routingDecision.Reason);

        var response = await CreateResponseAsync(
            routingDecision.BiblePassage,
            request.TheologicalLanguage,
            cancellationToken);

        yield return new TextDeltaEvent(response);

        yield return new AgentTurnCompletedEvent(
            routingDecision.AgentId,
            response);
    }

    private async IAsyncEnumerable<AgentRunEvent>
        CreateUnsupportedReferenceEventsAsync(
            RoutingDecision routingDecision,
            ApplicationLanguage theologicalLanguage,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;

        var response = theologicalLanguage ==
                ApplicationLanguage.English
            ? "I could not normalize this Bible reference reliably. " +
              "Check the book, chapter, and verses."
            : "Je n’ai pas pu normaliser cette référence biblique " +
              "de façon fiable. Vérifie le livre, le chapitre et les versets.";

        yield return new AgentSelectedEvent(
            routingDecision.AgentId,
            routingDecision.AgentName,
            routingDecision.Reason);

        yield return new TextDeltaEvent(response);

        yield return new AgentTurnCompletedEvent(
            routingDecision.AgentId,
            response);
    }

    private async Task<string> CreateResponseAsync(
        BiblePassageRequest request,
        ApplicationLanguage theologicalLanguage,
        CancellationToken cancellationToken)
    {
        var editionCode =
            request.RequestedEditionCode ??
            BibleEditionDefaults.For(
                theologicalLanguage);

        var verseLabel = request.VerseLabel;

        if (verseLabel is null)
        {
            return await CreateChapterResponseAsync(
                request,
                editionCode,
                cancellationToken);
        }

        if (request.EndVerseLabel is not null)
        {
            return await CreateRangeResponseAsync(
                request,
                editionCode,
                cancellationToken);
        }

        var editionBooks = await bibleRepository.GetBooksAsync(
            editionCode,
            cancellationToken);

        var book = editionBooks?.Books.FirstOrDefault(
            candidate =>
                string.Equals(
                    candidate.Code,
                    request.BookCode.Value,
                    StringComparison.Ordinal));

        if (editionBooks is null || book is null)
        {
            return CreateNotFoundResponse(
                request,
                editionCode,
                languageTag: null);
        }

        var verse = await bibleRepository.GetVerseAsync(
            editionCode,
            new BibleReference(
                request.BookCode,
                request.ChapterNumber,
                verseLabel),
            cancellationToken);

        if (verse is null)
        {
            return CreateNotFoundResponse(
                request,
                editionCode,
                editionBooks.Edition.LanguageTag);
        }

        return
            $"{book.DisplayName} " +
            $"{verse.ChapterNumber}:{verse.VerseLabel} " +
            $"({editionBooks.Edition.DisplayName})\n\n" +
            verse.Text +
            CreateProvenance(
                editionCode,
                editionBooks.Edition.LanguageTag);
    }

    private async Task<string> CreateChapterResponseAsync(
        BiblePassageRequest request,
        BibleEditionCode editionCode,
        CancellationToken cancellationToken)
    {
        var chapter = await bibleRepository.GetChapterAsync(
            editionCode,
            request.BookCode,
            request.ChapterNumber,
            cancellationToken);

        if (chapter is null)
        {
            return CreateNotFoundResponse(
                request,
                editionCode,
                languageTag: null);
        }

        var verses = string.Join(
            "\n",
            chapter.Verses.Select(
                verse => $"{verse.VerseLabel}. {verse.Text}"));

        return
            $"{chapter.Book.DisplayName} " +
            $"{chapter.ChapterNumber} " +
            $"({chapter.Edition.DisplayName})\n\n" +
            verses +
            CreateProvenance(
                editionCode,
                chapter.Edition.LanguageTag);
    }

    private async Task<string> CreateRangeResponseAsync(
        BiblePassageRequest request,
        BibleEditionCode editionCode,
        CancellationToken cancellationToken)
    {
        var chapter = await bibleRepository.GetChapterAsync(
            editionCode,
            request.BookCode,
            request.ChapterNumber,
            cancellationToken);

        if (chapter is null)
        {
            return CreateNotFoundResponse(
                request,
                editionCode,
                languageTag: null);
        }

        var firstVerse = chapter.Verses.FirstOrDefault(
            verse => string.Equals(
                verse.VerseLabel,
                request.VerseLabel,
                StringComparison.Ordinal));

        var lastVerse = chapter.Verses.FirstOrDefault(
            verse => string.Equals(
                verse.VerseLabel,
                request.EndVerseLabel,
                StringComparison.Ordinal));

        if (firstVerse is null ||
            lastVerse is null ||
            firstVerse.VerseOrdinal > lastVerse.VerseOrdinal)
        {
            return CreateNotFoundResponse(
                request,
                editionCode,
                chapter.Edition.LanguageTag);
        }

        var selectedVerses = chapter.Verses
            .Where(verse =>
                verse.VerseOrdinal >= firstVerse.VerseOrdinal &&
                verse.VerseOrdinal <= lastVerse.VerseOrdinal)
            .ToArray();

        var verses = string.Join(
            "\n",
            selectedVerses.Select(
                verse => $"{verse.VerseLabel}. {verse.Text}"));

        return
            $"{chapter.Book.DisplayName} " +
            $"{chapter.ChapterNumber}:" +
            $"{request.VerseLabel}-{request.EndVerseLabel} " +
            $"({chapter.Edition.DisplayName})\n\n" +
            verses +
            CreateProvenance(
                editionCode,
                chapter.Edition.LanguageTag);
    }

    private static string CreateProvenance(
        BibleEditionCode editionCode,
        string languageTag)
    {
        var label = string.Equals(
                languageTag,
                "en",
                StringComparison.OrdinalIgnoreCase)
            ? "Source: Bible corpus"
            : "Source : corpus biblique";

        return $"\n\n{label} · {editionCode} · PostgreSQL";
    }

    private static string CreateNotFoundResponse(
        BiblePassageRequest request,
        BibleEditionCode editionCode,
        string? languageTag)
    {
        var reference = request.VerseLabel is null
            ? $"{request.BookCode} {request.ChapterNumber}"
            : request.EndVerseLabel is null
                ? $"{request.BookCode} {request.ChapterNumber}:{request.VerseLabel}"
                : $"{request.BookCode} {request.ChapterNumber}:" +
                  $"{request.VerseLabel}-{request.EndVerseLabel}";

        return string.Equals(
                languageTag,
                "en",
                StringComparison.OrdinalIgnoreCase)
            ? $"Passage {reference} was not found " +
              $"in edition {editionCode}."
            : $"Le passage {reference} n’a pas été trouvé " +
              $"dans l’édition {editionCode}.";
    }

    private IAsyncEnumerable<AgentRunEvent> RunFallbackAsync(
        AgentTurnRequest request,
        RoutingDecision routingDecision,
        CancellationToken cancellationToken)
    {
        return fallbackRuntime is IRoutedAgentRuntime routedRuntime
            ? routedRuntime.RunTurnAsync(
                request,
                routingDecision,
                cancellationToken)
            : fallbackRuntime.RunTurnAsync(
                request,
                cancellationToken);
    }

}
