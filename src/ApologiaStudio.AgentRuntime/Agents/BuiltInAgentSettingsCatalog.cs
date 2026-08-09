using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed record BuiltInAgentSettingsDefinition(
    AgentDescriptor Agent,
    string Avatar,
    string BubbleColor,
    AgentPromptDefinition Prompt,
    string RoutingDescription);

public sealed class BuiltInAgentSettingsCatalog(
    AgentPromptCatalog promptCatalog)
{
    private readonly IReadOnlyList<BuiltInAgentSettingsDefinition> _all =
    [
        Create(
            BuiltInAgents.Historian,
            "🏛️",
            "#E7EEF4",
            promptCatalog),
        Create(
            BuiltInAgents.ProtestantApologist,
            "✝️",
            "#F0E9F6",
            promptCatalog)
    ];

    public IReadOnlyList<BuiltInAgentSettingsDefinition> All => _all;

    public BuiltInAgentSettingsDefinition Get(AgentId agentId)
    {
        return _all.FirstOrDefault(
                   definition => definition.Agent.Id == agentId)
               ?? throw new ArgumentException(
                   $"No defaults are configured for agent '{agentId}'.",
                   nameof(agentId));
    }

    private static BuiltInAgentSettingsDefinition Create(
        AgentDescriptor agent,
        string avatar,
        string bubbleColor,
        AgentPromptCatalog promptCatalog)
    {
        var routingProfile = BuiltInAgentRegistry.Profiles.Single(
            profile => profile.Agent.Id == agent.Id);
        return new BuiltInAgentSettingsDefinition(
            agent,
            avatar,
            bubbleColor,
            promptCatalog.Get(agent.Id),
            routingProfile.RoutingDescription);
    }
}
