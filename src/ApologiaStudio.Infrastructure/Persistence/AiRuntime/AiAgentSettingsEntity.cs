namespace ApologiaStudio.Infrastructure.Persistence.AiRuntime;

internal sealed class AiAgentSettingsEntity
{
    public Guid AgentId { get; set; }

    public string? Slug { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Avatar { get; set; } = string.Empty;

    public string BubbleColor { get; set; } = string.Empty;

    public string? Model { get; set; }

    public string SystemPrompt { get; set; } = string.Empty;

    public string? RoutingDescription { get; set; }

    public bool IsBuiltIn { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }
}
