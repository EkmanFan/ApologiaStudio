using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents;

namespace ApologiaStudio.AgentRuntime.Execution;

public interface IRoutedAgentRuntime : IAgentRuntime
{
    IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
        AgentTurnRequest request,
        RoutingDecision routingDecision,
        CancellationToken cancellationToken);
}
