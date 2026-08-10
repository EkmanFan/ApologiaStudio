using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Evaluations.Support;

internal static class LocalModelEvaluationSupport
{
    public static bool IsEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable(
                "OLLAMA_EVALUATIONS_ENABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static Uri GetBaseAddress()
    {
        var value =
            Environment.GetEnvironmentVariable(
                "OLLAMA_BASE_URL")
            ?? "http://127.0.0.1:11434";

        return new Uri(
            value.TrimEnd('/') + "/");
    }

    public static string GetRoutingModel() =>
        Environment.GetEnvironmentVariable(
            "OLLAMA_ROUTING_MODEL")
        ?? "qwen3:8b";

    public static string GetResponseModel() =>
        Environment.GetEnvironmentVariable(
            "OLLAMA_RESPONSE_MODEL")
        ?? "qwen3:8b";

    public static double? NanosecondsToMilliseconds(
        long? nanoseconds) =>
        nanoseconds is null
            ? null
            : nanoseconds.Value / 1_000_000d;
}

internal sealed class EvaluationAiRuntimeSettingsStore(
    AiRuntimeSettingsSnapshot settings)
    : IAiRuntimeSettingsStore
{
    public Task<AiRuntimeSettingsSnapshot?> GetAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<AiRuntimeSettingsSnapshot?>(settings);
    }

    public Task SaveAsync(
        AiRuntimeSettingsSnapshot settings,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class EvaluationAgentSettingsStore(
    IReadOnlyList<AgentSettingsSnapshot> settings)
    : IAgentSettingsStore
{
    public Task<IReadOnlyList<AgentSettingsSnapshot>> ListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(settings);
    }

    public Task<AgentSettingsSnapshot?> GetAsync(
        AgentId agentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            settings.FirstOrDefault(
                candidate =>
                    candidate.AgentId == agentId));
    }

    public Task<bool> TryCreateAsync(
        AgentSettingsSnapshot settings,
        int maximumActiveAgents,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task SaveAsync(
        AgentSettingsSnapshot settings,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> DeactivateAsync(
        AgentId agentId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class EvaluationOllamaHttpClientFactory
    : IOllamaHttpClientFactory
{
    public HttpClient Create(
        Uri baseAddress,
        TimeSpan timeout) =>
        new()
        {
            BaseAddress = baseAddress,
            Timeout = timeout
        };
}

internal sealed class RecordingOllamaRuntimeTelemetry
    : IOllamaRuntimeTelemetry
{
    public List<OllamaGenerationFirstTokenObservation>
        FirstTokens { get; } = [];

    public List<OllamaGenerationStartedObservation>
        Started { get; } = [];

    public List<OllamaGenerationCompletedObservation>
        Completed { get; } = [];

    public List<OllamaGenerationRejectedObservation>
        Rejected { get; } = [];

    public List<OllamaHistoryMessageSkippedObservation>
        HistorySkipped { get; } = [];

    public void GenerationFirstToken(
        OllamaGenerationFirstTokenObservation observation) =>
        FirstTokens.Add(observation);

    public void GenerationStarted(
        OllamaGenerationStartedObservation observation) =>
        Started.Add(observation);

    public void GenerationCompleted(
        OllamaGenerationCompletedObservation observation) =>
        Completed.Add(observation);

    public void GenerationRejected(
        OllamaGenerationRejectedObservation observation) =>
        Rejected.Add(observation);

    public void HistoryMessageSkipped(
        OllamaHistoryMessageSkippedObservation observation) =>
        HistorySkipped.Add(observation);
}
