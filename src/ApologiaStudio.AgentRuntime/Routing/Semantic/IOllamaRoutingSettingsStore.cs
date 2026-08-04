namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public interface IOllamaRoutingSettingsStore
{
    OllamaRoutingSettings Current { get; }

    Task SaveAsync(
        OllamaRoutingSettings settings,
        CancellationToken cancellationToken = default);
}
