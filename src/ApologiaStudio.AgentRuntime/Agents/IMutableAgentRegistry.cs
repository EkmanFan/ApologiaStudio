using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public interface IMutableAgentRegistry : IAgentRegistry
{
    void ReplaceAll(IEnumerable<AgentRoutingProfile> profiles);

    void Upsert(AgentRoutingProfile profile);

    bool Remove(AgentId agentId);
}
