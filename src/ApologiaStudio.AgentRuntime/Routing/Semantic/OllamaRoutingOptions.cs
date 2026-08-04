namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed class OllamaRoutingOptions
{
    public Uri BaseAddress { get; init; } = null!;

    public string Model { get; init; } = string.Empty;

    public TimeSpan RequestTimeout { get; init; }

    public string KeepAlive { get; init; } = string.Empty;
}
