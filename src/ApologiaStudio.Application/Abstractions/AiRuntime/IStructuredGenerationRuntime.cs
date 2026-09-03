namespace ApologiaStudio.Application.Abstractions.AiRuntime;

/// <summary>
/// One bounded, non-streaming generation constrained by a response schema.
///
/// Deliberately free of conversation and agent concepts: this capability is
/// reusable by any feature needing a structured answer, not only metadata
/// review.
/// </summary>
/// <param name="Purpose">
/// Short stable identifier of the calling feature, used for diagnostics.
/// </param>
/// <param name="ResponseSchema">
/// JSON Schema the provider must constrain its answer to. Transport assistance
/// only: the calling feature remains responsible for validating the result.
/// </param>
public sealed record StructuredGenerationRequest(
    string Purpose,
    string SystemPrompt,
    string UserPrompt,
    string ResponseSchema,
    string? ModelOverride = null,
    int? MaximumOutputTokens = null);

public sealed record StructuredGenerationResult(
    string Model,
    string Json,
    string? DoneReason,
    int? PromptTokenCount,
    int? OutputTokenCount,
    double DurationMilliseconds);

public interface IStructuredGenerationRuntime
{
    /// <summary>
    /// Honours cancellation and the configured generation timeout exactly as
    /// the streaming runtime does.
    /// </summary>
    Task<StructuredGenerationResult> GenerateAsync(
        StructuredGenerationRequest request,
        CancellationToken cancellationToken);
}

public sealed class StructuredGenerationException : Exception
{
    public StructuredGenerationException(string message)
        : base(message)
    {
    }

    public StructuredGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
