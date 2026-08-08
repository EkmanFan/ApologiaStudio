using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.Agents.Settings;

public sealed record UpdateAgentSettingsCommand(
    AgentId AgentId,
    string DisplayName,
    string Avatar,
    string BubbleColor,
    string? Model,
    string SystemPrompt);
