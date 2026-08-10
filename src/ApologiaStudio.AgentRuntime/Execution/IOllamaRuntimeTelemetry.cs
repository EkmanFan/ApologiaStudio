namespace ApologiaStudio.AgentRuntime.Execution;

public interface IOllamaRuntimeTelemetry
{
    void GenerationFirstToken(
        OllamaGenerationFirstTokenObservation observation);

    void GenerationStarted(
        OllamaGenerationStartedObservation observation);

    void GenerationCompleted(
        OllamaGenerationCompletedObservation observation);

    void GenerationRejected(
        OllamaGenerationRejectedObservation observation);

    void HistoryMessageSkipped(
        OllamaHistoryMessageSkippedObservation observation);
}
