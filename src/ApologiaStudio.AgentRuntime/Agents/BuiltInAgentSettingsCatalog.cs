using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed record BuiltInAgentSettingsDefinition(
    AgentDescriptor Agent,
    string Avatar,
    string BubbleColor,
    AgentPromptDefinition Prompt);

public sealed class BuiltInAgentSettingsCatalog(
    AgentPromptCatalog promptCatalog)
{
    private readonly IReadOnlyList<BuiltInAgentSettingsDefinition> _all =
    [
        new(
            BuiltInAgents.Historian,
            "🏛️",
            "#E7EEF4",
            promptCatalog.Get(BuiltInAgents.Historian.Id)),
        new(
            BuiltInAgents.ProtestantApologist,
            "✝️",
            "#F0E9F6",
            promptCatalog.Get(BuiltInAgents.ProtestantApologist.Id))
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
}
