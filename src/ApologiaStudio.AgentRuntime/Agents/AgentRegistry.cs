using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed class AgentRegistry : IMutableAgentRegistry
{
    private readonly object _sync = new();
    private AgentRoutingProfile[] _all;

    public AgentRegistry()
        : this(BuiltInAgentRegistry.Profiles)
    {
    }

    public AgentRegistry(IEnumerable<AgentRoutingProfile> profiles)
    {
        _all = Validate(profiles);
    }

    public IReadOnlyList<AgentRoutingProfile> All =>
        Volatile.Read(ref _all);

    public bool TryGet(
        AgentId agentId,
        out AgentRoutingProfile profile)
    {
        var result = Volatile.Read(ref _all)
            .FirstOrDefault(candidate => candidate.Agent.Id == agentId);
        if (result is null)
        {
            profile = null!;
            return false;
        }

        profile = result;
        return true;
    }

    public bool TryGet(
        string slug,
        out AgentRoutingProfile profile)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            profile = null!;
            return false;
        }

        var result = Volatile.Read(ref _all)
            .FirstOrDefault(
                candidate => string.Equals(
                    candidate.Agent.Slug,
                    slug,
                    StringComparison.OrdinalIgnoreCase));
        if (result is null)
        {
            profile = null!;
            return false;
        }

        profile = result;
        return true;
    }

    public void ReplaceAll(IEnumerable<AgentRoutingProfile> profiles)
    {
        var validated = Validate(profiles);
        lock (_sync)
        {
            Volatile.Write(ref _all, validated);
        }
    }

    public void Upsert(AgentRoutingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        lock (_sync)
        {
            var current = Volatile.Read(ref _all);
            var updated = current
                .Where(candidate => candidate.Agent.Id != profile.Agent.Id)
                .Append(profile)
                .ToArray();
            Volatile.Write(ref _all, Validate(updated));
        }
    }

    public bool Remove(AgentId agentId)
    {
        if (BuiltInAgents.TryGet(agentId, out _))
        {
            return false;
        }

        lock (_sync)
        {
            var current = Volatile.Read(ref _all);
            var updated = current
                .Where(candidate => candidate.Agent.Id != agentId)
                .ToArray();
            if (updated.Length == current.Length)
            {
                return false;
            }

            Volatile.Write(ref _all, Validate(updated));
            return true;
        }
    }

    private static AgentRoutingProfile[] Validate(
        IEnumerable<AgentRoutingProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        return new BuiltInAgentRegistry(profiles)
            .All
            .ToArray();
    }
}
