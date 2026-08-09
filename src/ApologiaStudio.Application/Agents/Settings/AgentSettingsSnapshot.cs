using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.Agents.Settings;

public sealed record AgentSettingsSnapshot(
    AgentId AgentId,
    string Slug,
    string DisplayName,
    string Avatar,
    string BubbleColor,
    string? Model,
    string SystemPrompt,
    string RoutingDescription,
    bool IsBuiltIn,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);
