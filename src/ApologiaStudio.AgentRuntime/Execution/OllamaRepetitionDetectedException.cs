namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class OllamaRepetitionDetectedException(
    int generatedCharacterCount,
    int repeatedPatternLength,
    int repeatCount)
    : InvalidOperationException(
        "Ollama generation was stopped because a repeated-output " +
        "loop was detected.")
{
    public int GeneratedCharacterCount { get; } =
        generatedCharacterCount;

    public int RepeatedPatternLength { get; } =
        repeatedPatternLength;

    public int RepeatCount { get; } =
        repeatCount;
}
