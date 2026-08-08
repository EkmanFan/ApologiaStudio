namespace ApologiaStudio.Infrastructure.Persistence.AiRuntime;

internal sealed class AiAgentSettingsEntity
{
    public Guid AgentId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Avatar { get; set; } = string.Empty;

    public string BubbleColor { get; set; } = string.Empty;

    public string? Model { get; set; }

    public string SystemPrompt { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
