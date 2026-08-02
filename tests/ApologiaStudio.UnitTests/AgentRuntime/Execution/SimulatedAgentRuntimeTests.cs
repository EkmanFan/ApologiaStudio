using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Execution;

public sealed class SimulatedAgentRuntimeTests
{
    private readonly SimulatedAgentRuntime _runtime = new(
        new DeterministicAgentRouter(),
        new SimulatedAgentResponseProvider());

    [Fact]
    public async Task RunTurnAsync_ShouldRouteHistoricalQuestionToHistorian()
    {
        var request = CreateRequest(
            "Quand cette doctrine est-elle apparue dans l'histoire ?");

        var events = await CollectEventsAsync(request);

        var selected = Assert.IsType<AgentSelectedEvent>(
            events[0]);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            selected.AgentId);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            completed.AgentId);

        Assert.Contains(
            "Historian simulation",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldRouteApologeticQuestionToApologist()
    {
        var request = CreateRequest(
            "Comment défendre la résurrection face à une objection athée ?");

        var events = await CollectEventsAsync(request);

        var selected = Assert.IsType<AgentSelectedEvent>(
            events[0]);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            selected.AgentId);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Contains(
            "Protestant apologist simulation",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldRespectExplicitAgentSelection()
    {
        var request = CreateRequest(
            "Comment défendre la résurrection ?",
            BuiltInAgents.Historian.Id);

        var events = await CollectEventsAsync(request);

        var selected = Assert.IsType<AgentSelectedEvent>(
            events[0]);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            selected.AgentId);

        Assert.Contains(
            "explicitly selected",
            selected.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldEmitTextDeltaEvents()
    {
        var request = CreateRequest(
            "Quand cette doctrine est-elle apparue ?");

        var events = await CollectEventsAsync(request);

        var textEvents = events
            .OfType<TextDeltaEvent>()
            .ToArray();

        Assert.NotEmpty(textEvents);

        var streamedText = string.Concat(
            textEvents.Select(agentEvent => agentEvent.Content));

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Equal(
            completed.Content,
            streamedText);
    }

    private async Task<List<AgentRunEvent>> CollectEventsAsync(
        AgentTurnRequest request)
    {
        var events = new List<AgentRunEvent>();

        await foreach (var agentEvent in _runtime.RunTurnAsync(
                           request,
                           CancellationToken.None))
        {
            events.Add(agentEvent);
        }

        return events;
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
}
