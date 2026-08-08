namespace ApologiaStudio.Application.AiRuntime.Settings;

public sealed record AiRuntimeSettingsDefaults(
    string BaseAddress,
    string RoutingModel,
    string DefaultAgentModel,
    int RoutingTimeoutSeconds,
    int GenerationTimeoutSeconds,
    string KeepAlive,
    int MaximumHistoryMessages,
    int MaximumHistoryCharacters,
    int MaximumOutputTokens);
