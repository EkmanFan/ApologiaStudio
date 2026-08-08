namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public interface IOllamaModelCatalogClient
{
    Task<IReadOnlyList<OllamaLocalModel>> ListLocalModelsAsync(
        Uri baseAddress,
        CancellationToken cancellationToken = default);
}
