namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class OllamaGenerationOptions
{
    public Uri BaseAddress { get; init; } =
        new("http://127.0.0.1:11434/");

    public string Model { get; init; } =
        "qwen3:8b";

    public TimeSpan RequestTimeout { get; init; } =
        TimeSpan.FromSeconds(180);

    public string KeepAlive { get; init; } =
        "10m";

    public int MaximumHistoryMessages { get; init; } =
        24;

    public int MaximumHistoryCharacters { get; init; } =
        24_000;

    public int MaximumOutputTokens { get; init; } =
        1_200;
}
