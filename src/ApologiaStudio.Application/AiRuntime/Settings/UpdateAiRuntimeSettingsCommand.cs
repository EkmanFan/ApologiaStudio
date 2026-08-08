namespace ApologiaStudio.Application.AiRuntime.Settings;

public sealed record UpdateAiRuntimeSettingsCommand(
    string BaseAddress,
    string RoutingModel,
    string DefaultAgentModel,
    int RoutingTimeoutSeconds,
    int GenerationTimeoutSeconds,
    string KeepAlive,
    int MaximumHistoryMessages,
    int MaximumHistoryCharacters,
    int MaximumOutputTokens,
    IReadOnlyList<AgentModelAssignmentInput> AgentModels);
