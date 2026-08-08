namespace ApologiaStudio.Infrastructure.Persistence.AiRuntime;

internal sealed class AiAgentModelAssignmentEntity
{
    public string Provider { get; set; } = string.Empty;

    public Guid AgentId { get; set; }

    public string Model { get; set; } = string.Empty;
}
