namespace ApologiaStudio.Infrastructure.Persistence.AiRuntime;

internal sealed class AiRuntimeSettingsEntity
{
    public string Provider { get; set; } = string.Empty;

    public string BaseAddress { get; set; } = string.Empty;

    public string RoutingModel { get; set; } = string.Empty;

    public string DefaultAgentModel { get; set; } = string.Empty;

    public int RoutingTimeoutSeconds { get; set; }

    public int GenerationTimeoutSeconds { get; set; }

    public string KeepAlive { get; set; } = string.Empty;

    public int MaximumHistoryMessages { get; set; }

    public int MaximumHistoryCharacters { get; set; }

    public int MaximumOutputTokens { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<AiAgentModelAssignmentEntity> AgentModels { get; } = [];
}
