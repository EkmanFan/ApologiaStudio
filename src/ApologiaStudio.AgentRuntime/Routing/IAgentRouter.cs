using ApologiaStudio.Application.Agents;

namespace ApologiaStudio.AgentRuntime.Routing;

public interface IAgentRouter
{
    RoutingDecision Route(AgentTurnRequest request);
}
