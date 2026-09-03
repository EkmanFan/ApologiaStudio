using ApologiaStudio.AgentRuntime.Execution;

namespace ApologiaStudio.Web.AiRuntime;

public sealed class LoggingStructuredGenerationTelemetry(
    ILogger<LoggingStructuredGenerationTelemetry> logger)
    : IStructuredGenerationTelemetry
{
    public void GenerationStarted(
        StructuredGenerationStartedObservation observation)
    {
        logger.LogInformation(
            "Structured generation started. " +
            "Purpose={Purpose} Model={Model} MaximumOutputTokens={MaximumOutputTokens}",
            observation.Purpose,
            observation.Model,
            observation.MaximumOutputTokens);
    }

    public void GenerationCompleted(
        StructuredGenerationCompletedObservation observation)
    {
        logger.LogInformation(
            "Structured generation completed. " +
            "Purpose={Purpose} Model={Model} DoneReason={DoneReason} " +
            "PromptTokens={PromptTokens} OutputTokens={OutputTokens} " +
            "DurationMs={DurationMs:F1}",
            observation.Purpose,
            observation.Model,
            observation.DoneReason,
            observation.PromptTokenCount,
            observation.OutputTokenCount,
            observation.DurationMilliseconds);
    }

    public void GenerationFailed(
        StructuredGenerationFailedObservation observation)
    {
        logger.LogWarning(
            "Structured generation failed. " +
            "Purpose={Purpose} Model={Model} Reason={Reason} DurationMs={DurationMs:F1}",
            observation.Purpose,
            observation.Model,
            observation.Reason,
            observation.DurationMilliseconds);
    }
}
