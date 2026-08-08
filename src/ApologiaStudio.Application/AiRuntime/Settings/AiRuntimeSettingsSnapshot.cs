using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.AiRuntime.Settings;

public sealed record AiRuntimeSettingsSnapshot(
    string Provider,
    string BaseAddress,
    string RoutingModel,
    string DefaultAgentModel,
    int RoutingTimeoutSeconds,
    int GenerationTimeoutSeconds,
    string KeepAlive,
    int MaximumHistoryMessages,
    int MaximumHistoryCharacters,
    int MaximumOutputTokens,
    DateTimeOffset UpdatedAt,
    IReadOnlyDictionary<Guid, string> AgentModels)
{
    public const string OllamaProvider = "Ollama";

    public string ResolveAgentModel(AgentId agentId)
    {
        return AgentModels.TryGetValue(
                agentId.Value,
                out var model)
            ? model
            : DefaultAgentModel;
    }
}
