using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly BuiltInAgentRegistry _inner;

    public AgentRegistry()
        : this(BuiltInAgentRegistry.Profiles)
    {
    }

    public AgentRegistry(IEnumerable<AgentRoutingProfile> profiles)
    {
        _inner = new BuiltInAgentRegistry(profiles);
    }

    public IReadOnlyList<AgentRoutingProfile> All => _inner.All;

    public bool TryGet(
        AgentId agentId,
        out AgentRoutingProfile profile)
    {
        return _inner.TryGet(agentId, out profile);
    }

    public bool TryGet(
        string slug,
        out AgentRoutingProfile profile)
    {
        return _inner.TryGet(slug, out profile);
    }
}
