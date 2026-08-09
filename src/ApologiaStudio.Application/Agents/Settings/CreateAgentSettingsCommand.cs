namespace ApologiaStudio.Application.Agents.Settings;

public sealed record CreateAgentSettingsCommand(
    string DisplayName,
    string Avatar,
    string BubbleColor,
    string? Model,
    string SystemPrompt,
    string RoutingDescription);
