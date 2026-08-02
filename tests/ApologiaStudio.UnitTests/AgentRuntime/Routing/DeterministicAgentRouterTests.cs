using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Routing;

public sealed class DeterministicAgentRouterTests
{
    private readonly DeterministicAgentRouter _router = new();

    [Fact]
    public void Route_ShouldUseExplicitlyRequestedAgent()
    {
        var request = CreateRequest(
            "Comment défendre la résurrection ?",
            BuiltInAgents.Historian.Id);

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);

        Assert.True(decision.WasExplicitlyRequested);
        Assert.Equal(1.0, decision.Confidence);
    }

    [Fact]
    public void Route_ShouldRejectUnknownRequestedAgent()
    {
        var unknownAgentId = AgentId.New();

        var request = CreateRequest(
            "A question",
            unknownAgentId);

        Assert.Throws<ArgumentException>(
            () => _router.Route(request));
    }

    [Fact]
    public void Route_ShouldSelectHistorianForFrenchHistoricalQuestion()
    {
        var request = CreateRequest(
            "À quelle époque apparaissent les premières preuves historiques de la primauté de Rome ?");

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);

        Assert.False(decision.WasExplicitlyRequested);
    }

    [Fact]
    public void Route_ShouldSelectHistorianForEnglishHistoricalQuestion()
    {
        var request = CreateRequest(
            "When did this doctrine emerge in church history?");

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);
    }

    [Fact]
    public void Route_ShouldSelectApologistForFrenchApologeticQuestion()
    {
        var request = CreateRequest(
            "Comment défendre la résurrection face à une objection athée ?");

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);
    }

    [Fact]
    public void Route_ShouldUseApologistAsDefault()
    {
        var request = CreateRequest(
            "Peux-tu m'aider avec ce sujet ?");

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);

        Assert.Equal(0.55, decision.Confidence);
    }

    [Fact]
    public void Route_ShouldUseCurrentMessageInsteadOfEarlierHistory()
    {
        var previousMessage = new ConversationMessageContext(
            MessageId.New(),
            MessageRole.User,
            "Quand cette doctrine est-elle apparue dans l'histoire ?",
            AgentId: null,
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var currentMessageId = MessageId.New();

        var currentMessage = new ConversationMessageContext(
            currentMessageId,
            MessageRole.User,
            "Comment défendre la résurrection contre une objection athée ?",
            AgentId: null,
            DateTimeOffset.UtcNow);

        var request = new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            currentMessageId,
            RequestedAgentId: null,
            History:
            [
                previousMessage,
                currentMessage
            ]);

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);
    }

    private static AgentTurnRequest CreateRequest(
        string content,
        AgentId? requestedAgentId = null)
    {
        var messageId = MessageId.New();

        var message = new ConversationMessageContext(
            messageId,
            MessageRole.User,
            content,
            AgentId: null,
            DateTimeOffset.UtcNow);

        return new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            messageId,
            requestedAgentId,
            History: [message]);
    }
}
