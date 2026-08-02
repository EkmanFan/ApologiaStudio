using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Routing;

public sealed class HybridAgentRouterTests
{
    [Fact]
    public async Task RouteAsync_ShouldRespectExplicitSelection()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "protestant-apologist",
                0.99,
                "Should not be used."));

        var router = CreateRouter(classifier);

        var request = CreateRequest(
            "Comment défendre la résurrection ?",
            BuiltInAgents.Historian.Id);

        var decision =
            await router.RouteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);

        Assert.True(
            decision.WasExplicitlyRequested);

        Assert.Equal(
            0,
            classifier.CallCount);
    }

    [Fact]
    public async Task RouteAsync_ShouldKeepStrongDeterministicDecision()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "protestant-apologist",
                0.99,
                "Should not be used."));

        var router = CreateRouter(classifier);

        var request = CreateRequest(
            "À quelle époque cette doctrine est-elle apparue dans l'histoire ?");

        var decision =
            await router.RouteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);

        Assert.Equal(
            0,
            classifier.CallCount);
    }

    [Fact]
    public async Task RouteAsync_ShouldNotUseSemanticRoutingForBibleReference()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "historian",
                0.99,
                "Should not be used."));

        var router = CreateRouter(classifier);

        var decision = await router.RouteAsync(
            CreateRequest("Donne-moi Jean 3:16."),
            CancellationToken.None);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);

        Assert.Equal(0, classifier.CallCount);
    }

    [Fact]
    public async Task RouteAsync_ShouldNotUseSemanticRoutingForWholeBibleChapter()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "historian",
                0.99,
                "Should not be used."));

        var router = CreateRouter(classifier);

        var decision = await router.RouteAsync(
            CreateRequest("Donne-moi 1 Corinthiens 13."),
            CancellationToken.None);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);

        Assert.Equal(0, classifier.CallCount);
    }

    [Fact]
    public async Task RouteAsync_ShouldCarryNormalizedMisspelledBibleReference()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "protestant-apologist",
                0.98,
                "La référence biblique a été normalisée.",
                BiblePassageResolution.Resolved,
                new BiblePassageRequest(
                    new BibleEditionCode("lsg1910"),
                    new UsfmBookCode("1CO"),
                    13,
                    VerseLabel: null)));

        var router = CreateRouter(classifier);

        var decision = await router.RouteAsync(
            CreateRequest("Donne-moi 1 Corinthien 13."),
            CancellationToken.None);

        Assert.Equal(1, classifier.CallCount);
        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);
        Assert.Equal(
            BiblePassageResolution.Resolved,
            decision.BiblePassageResolution);
        Assert.Equal("1CO", decision.BiblePassage?.BookCode.Value);
        Assert.Equal(13, decision.BiblePassage?.ChapterNumber);
    }

    [Fact]
    public async Task RouteAsync_ShouldNormalizeReferenceWithExplicitApologist()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "protestant-apologist",
                0.98,
                "La référence biblique a été normalisée.",
                BiblePassageResolution.Resolved,
                new BiblePassageRequest(
                    new BibleEditionCode("lsg1910"),
                    new UsfmBookCode("1CO"),
                    13,
                    VerseLabel: null)));

        var router = CreateRouter(classifier);

        var decision = await router.RouteAsync(
            CreateRequest(
                "Donne-moi 1 Corinthien 13.",
                BuiltInAgents.ProtestantApologist.Id),
            CancellationToken.None);

        Assert.Equal(1, classifier.CallCount);
        Assert.True(decision.WasExplicitlyRequested);
        Assert.Equal(
            BiblePassageResolution.Resolved,
            decision.BiblePassageResolution);
        Assert.Equal("1CO", decision.BiblePassage?.BookCode.Value);
    }

    [Fact]
    public async Task RouteAsync_ShouldUseSemanticIntentForBibleExegesis()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "protestant-apologist",
                0.97,
                "La demande porte sur l’interprétation du passage."));

        var router = CreateRouter(classifier);

        var decision = await router.RouteAsync(
            CreateRequest("Explique-moi Jean 3:16."),
            CancellationToken.None);

        Assert.Equal(1, classifier.CallCount);
        Assert.Equal(
            BiblePassageResolution.None,
            decision.BiblePassageResolution);
    }

    [Fact]
    public async Task RouteAsync_ShouldBlockInvalidBibleNormalization()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "protestant-apologist",
                0.40,
                "La demande ressemble à une référence biblique.",
                BiblePassageResolution.Unsupported));

        var router = CreateRouter(classifier);

        var decision = await router.RouteAsync(
            CreateRequest("Donne-moi 9 Corinthien 999."),
            CancellationToken.None);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);
        Assert.Equal(
            BiblePassageResolution.Unsupported,
            decision.BiblePassageResolution);
        Assert.Null(decision.BiblePassage);
    }

    [Fact]
    public async Task RouteAsync_ShouldBlockCandidateMisclassifiedAsGeneral()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "protestant-apologist",
                0.80,
                "Classification générale sans référence."));

        var router = CreateRouter(classifier);

        var decision = await router.RouteAsync(
            CreateRequest("1 Corinthien 13"),
            CancellationToken.None);

        Assert.Equal(
            BiblePassageResolution.Unsupported,
            decision.BiblePassageResolution);
        Assert.Null(decision.BiblePassage);
    }

    [Fact]
    public async Task RouteAsync_ShouldBlockCandidateWhenClassifierFails()
    {
        var router = CreateRouter(
            new ThrowingSemanticClassifier());

        var decision = await router.RouteAsync(
            CreateRequest("1 Corinthien 13"),
            CancellationToken.None);

        Assert.Equal(
            BiblePassageResolution.Unsupported,
            decision.BiblePassageResolution);
        Assert.Contains(
            "normalization was unavailable",
            decision.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RouteAsync_ShouldUseSemanticRoutingForClovisQuestion()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "historian",
                0.98,
                "La question porte sur l’âge d’un personnage lors d’un événement historique."));

        var router = CreateRouter(classifier);

        var request = CreateRequest(
            "Quel âge avait Clovis lors de son sacre ?");

        var decision =
            await router.RouteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);

        Assert.Equal(
            1,
            classifier.CallCount);

        Assert.Equal(
            "La question porte sur l’âge d’un personnage lors d’un événement historique.",
            decision.Reason);
    }

    [Fact]
    public async Task RouteAsync_ShouldRejectLowSemanticConfidence()
    {
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                "historian",
                0.40,
                "Classification incertaine."));

        var router = CreateRouter(classifier);

        var decision =
            await router.RouteAsync(
                CreateRequest(
                    "Aide-moi avec ce sujet."),
                CancellationToken.None);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);

        Assert.Contains(
            "insufficient",
            decision.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RouteAsync_ShouldFallbackWhenClassifierFails()
    {
        var classifier =
            new ThrowingSemanticClassifier();

        var router = CreateRouter(classifier);

        var decision =
            await router.RouteAsync(
                CreateRequest(
                    "Quel âge avait Clovis lors de son sacre ?"),
                CancellationToken.None);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);

        Assert.Contains(
            "unavailable",
            decision.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    private static HybridAgentRouter CreateRouter(
        ISemanticRoutingClassifier classifier)
    {
        return new HybridAgentRouter(
            new DeterministicAgentRouter(),
            classifier,
            new HybridRoutingOptions());
    }

    private static AgentTurnRequest CreateRequest(
        string content,
        AgentId? requestedAgentId = null)
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
            ]);
    }

    private sealed class StubSemanticClassifier(
        SemanticRoutingResult result)
        : ISemanticRoutingClassifier
    {
        public int CallCount { get; private set; }

        public ValueTask<SemanticRoutingResult> ClassifyAsync(
            string userMessage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingSemanticClassifier
        : ISemanticRoutingClassifier
    {
        public ValueTask<SemanticRoutingResult> ClassifyAsync(
            string userMessage,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Simulated provider failure.");
        }
    }
}
