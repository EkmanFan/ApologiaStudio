using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Routing;

public sealed class TelemetryAgentRouterTests
{
    [Fact]
    public async Task RouteAsync_ShouldRecordCompletedRouting()
    {
        var decision =
            new RoutingDecision(
                BuiltInAgents.Historian.Id,
                BuiltInAgents.Historian.DisplayName,
                "Test routing decision.",
                1.0,
                WasExplicitlyRequested: true);

        var inner =
            new StubAgentRouter(decision);

        var telemetry =
            new RecordingAgentRoutingTelemetry();

        var router =
            new TelemetryAgentRouter(
                inner,
                telemetry);

        var request =
            CreateRequest(
                BuiltInAgents.Historian.Id);

        var actual =
            await router.RouteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(decision, actual);
        Assert.Equal(1, inner.CallCount);

        var observation =
            Assert.Single(telemetry.Completed);

        Assert.Equal(
            request.ConversationId,
            observation.ConversationId);

        Assert.Equal(
            request.RequestedAgentId,
            observation.RequestedAgentId);

        Assert.Equal(
            decision.AgentId,
            observation.SelectedAgentId);

        Assert.Equal(
            decision.WasExplicitlyRequested,
            observation.WasExplicitlyRequested);

        Assert.True(
            observation.DurationMilliseconds >= 0);
    }

    private static AgentTurnRequest CreateRequest(
        AgentId? requestedAgentId)
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
                    "Quel âge avait Clovis lors de son sacre ?",
                    AgentId: null,
                    DateTimeOffset.UtcNow)
            ]);
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

    private sealed class RecordingAgentRoutingTelemetry
        : IAgentRoutingTelemetry
    {
        public List<AgentRoutingCompletedObservation>
            Completed { get; } = [];

        public void RoutingCompleted(
            AgentRoutingCompletedObservation observation)
        {
            Completed.Add(observation);
        }
    }
}
