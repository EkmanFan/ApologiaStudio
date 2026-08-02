namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed class OllamaRoutingOptions
{
    public Uri BaseAddress { get; init; } =
        new("http://127.0.0.1:11434/");

    public string Model { get; init; } =
        "qwen3:8b";

    public TimeSpan RequestTimeout { get; init; } =
        TimeSpan.FromSeconds(60);

    public string KeepAlive { get; init; } =
        "10m";
}
