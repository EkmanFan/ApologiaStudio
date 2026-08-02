using ApologiaStudio.Application.Agents;

namespace ApologiaStudio.AgentRuntime.Routing;

public interface IAgentRouter
{
    ValueTask<RoutingDecision> RouteAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken);
}
