namespace ApologiaStudio.AgentRuntime.Execution;

/// <summary>
/// Diagnostics for schema-constrained generation. Kept separate from the
/// conversational telemetry because a structured run has no conversation or
/// agent identity to report, not because it deserves weaker observability.
/// </summary>
public interface IStructuredGenerationTelemetry
{
    void GenerationStarted(
        StructuredGenerationStartedObservation observation);

    void GenerationCompleted(
        StructuredGenerationCompletedObservation observation);

    void GenerationFailed(
        StructuredGenerationFailedObservation observation);
}

public sealed record StructuredGenerationStartedObservation(
    string Purpose,
    string Model,
    int MaximumOutputTokens);

public sealed record StructuredGenerationCompletedObservation(
    string Purpose,
    string Model,
    string? DoneReason,
    int? PromptTokenCount,
    int? OutputTokenCount,
    double DurationMilliseconds);

public sealed record StructuredGenerationFailedObservation(
    string Purpose,
    string Model,
    string Reason,
    double DurationMilliseconds);
