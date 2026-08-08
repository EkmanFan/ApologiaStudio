namespace ApologiaStudio.Application.AiRuntime.Settings;

public sealed record AgentModelAssignmentInput(
    Guid AgentId,
    string? Model);
