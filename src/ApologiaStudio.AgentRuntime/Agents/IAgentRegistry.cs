using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public interface IAgentRegistry
{
    IReadOnlyList<AgentRoutingProfile> All { get; }

    bool TryGet(
        AgentId agentId,
        out AgentRoutingProfile profile);

    bool TryGet(
        string slug,
        out AgentRoutingProfile profile);
}
