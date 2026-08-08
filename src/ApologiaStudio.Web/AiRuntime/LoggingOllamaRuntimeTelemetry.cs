using ApologiaStudio.AgentRuntime.Execution;

namespace ApologiaStudio.Web.AiRuntime;

public sealed class LoggingOllamaRuntimeTelemetry(
    ILogger<LoggingOllamaRuntimeTelemetry> logger)
    : IOllamaRuntimeTelemetry
{
    public void GenerationStarted(
        OllamaGenerationStartedObservation observation)
    {
        logger.LogInformation(
            "Ollama generation started. " +
            "ConversationId={ConversationId} AgentId={AgentId} " +
            "Model={Model} HistoryMessageCount={HistoryMessageCount} " +
            "MaximumOutputTokens={MaximumOutputTokens}",
            observation.ConversationId.Value,
            observation.AgentId.Value,
            observation.Model,
            observation.HistoryMessageCount,
            observation.MaximumOutputTokens);
    }

    public void GenerationCompleted(
        OllamaGenerationCompletedObservation observation)
    {
        var logLevel =
            string.Equals(
                observation.DoneReason,
                "stop",
                StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Information
                : LogLevel.Warning;

        logger.Log(
            logLevel,
            "Ollama generation completed. " +
            "ConversationId={ConversationId} AgentId={AgentId} " +
            "Model={Model} DoneReason={DoneReason} " +
            "PromptTokenCount={PromptTokenCount} " +
            "OutputTokenCount={OutputTokenCount} " +
            "TotalDurationMs={TotalDurationMs} " +
            "LoadDurationMs={LoadDurationMs} " +
            "PromptEvaluationDurationMs={PromptEvaluationDurationMs} " +
            "EvaluationDurationMs={EvaluationDurationMs}",
            observation.ConversationId.Value,
            observation.AgentId.Value,
            observation.Model,
            observation.DoneReason,
            observation.PromptTokenCount,
            observation.OutputTokenCount,
            ToMilliseconds(observation.TotalDurationNanoseconds),
            ToMilliseconds(observation.LoadDurationNanoseconds),
            ToMilliseconds(
                observation.PromptEvaluationDurationNanoseconds),
            ToMilliseconds(observation.EvaluationDurationNanoseconds));
    }

    public void GenerationRejected(
        OllamaGenerationRejectedObservation observation)
    {
        logger.LogWarning(
            "Ollama generation rejected after repeated output was " +
            "detected. ConversationId={ConversationId} " +
            "AgentId={AgentId} Model={Model} " +
            "GeneratedCharacterCount={GeneratedCharacterCount} " +
            "RepeatedPatternLength={RepeatedPatternLength} " +
            "RepeatCount={RepeatCount}",
            observation.ConversationId.Value,
            observation.AgentId.Value,
            observation.Model,
            observation.GeneratedCharacterCount,
            observation.RepeatedPatternLength,
            observation.RepeatCount);
    }

    public void HistoryMessageSkipped(
        OllamaHistoryMessageSkippedObservation observation)
    {
        logger.LogWarning(
            "A repetitive assistant history message was excluded from " +
            "the Ollama prompt. ConversationId={ConversationId} " +
            "MessageId={MessageId} AgentId={AgentId} " +
            "CharacterCount={CharacterCount} " +
            "RepeatedPatternLength={RepeatedPatternLength} " +
            "RepeatCount={RepeatCount}",
            observation.ConversationId.Value,
            observation.MessageId.Value,
            observation.AgentId?.Value,
            observation.CharacterCount,
            observation.RepeatedPatternLength,
            observation.RepeatCount);
    }

    private static double? ToMilliseconds(
        long? nanoseconds)
    {
        return nanoseconds is null
            ? null
            : nanoseconds.Value / 1_000_000d;
    }
}
