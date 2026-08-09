using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed class BuiltInAgentRegistry : IAgentRegistry
{
    public static IReadOnlyList<AgentRoutingProfile> Profiles { get; } =
        Array.AsReadOnly(
        [
            new AgentRoutingProfile(
                BuiltInAgents.Historian,
                """
                - historical people, rulers, events and institutions;
                - chronology, dates, durations and ages at historical events;
                - councils, political history and Church history;
                - development of doctrines or practices through history;
                - descriptive questions about what happened historically.
                """),
            new AgentRoutingProfile(
                BuiltInAgents.ProtestantApologist,
                """
                - defence of Christian or Protestant beliefs;
                - biblical doctrine and theological interpretation;
                - objections from atheism, Islam, Catholicism or Orthodoxy;
                - arguments for God, Christ, resurrection or Scripture;
                - normative questions about what Christians should believe.
                """)
        ]);

    private readonly IReadOnlyList<AgentRoutingProfile> _all;

    public BuiltInAgentRegistry()
        : this(Profiles)
    {
    }

    public BuiltInAgentRegistry(
        IEnumerable<AgentRoutingProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var items = profiles.ToArray();
        if (items.Length == 0)
        {
            throw new ArgumentException(
                "At least one agent must be registered.",
                nameof(profiles));
        }
        if (items.Any(
                profile =>
                    string.IsNullOrWhiteSpace(profile.Agent.Slug) ||
                    string.IsNullOrWhiteSpace(profile.Agent.DisplayName) ||
                    string.IsNullOrWhiteSpace(profile.RoutingDescription)))
        {
            throw new ArgumentException(
                "Every registered agent must define a slug, display name and routing description.",
                nameof(profiles));
        }
        if (items
            .GroupBy(profile => profile.Agent.Id)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Agent identifiers must be unique.",
                nameof(profiles));
        }
        if (items
            .GroupBy(
                profile => profile.Agent.Slug,
                StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Agent slugs must be unique.",
                nameof(profiles));
        }

        _all = Array.AsReadOnly(items);
    }

    public IReadOnlyList<AgentRoutingProfile> All => _all;

    public bool TryGet(
        AgentId agentId,
        out AgentRoutingProfile profile)
    {
        var result = _all.FirstOrDefault(
            candidate => candidate.Agent.Id == agentId);
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

        var result = _all.FirstOrDefault(
            candidate =>
                string.Equals(
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
}
