namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed record OllamaRoutingSettings(
    string BaseAddress,
    string Model,
    int RequestTimeoutSeconds,
    string KeepAlive);
