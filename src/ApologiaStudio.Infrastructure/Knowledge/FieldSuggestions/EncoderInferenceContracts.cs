namespace ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

/// <summary>
/// One inference over one resident encoder.
/// </summary>
/// <param name="ModelId">
/// Logical model identity, never a filesystem path: where the artifact lives is
/// the worker's configuration, not Apologia's.
/// </param>
public sealed record EncoderInferenceRequest(
    string ModelId,
    string SerializedInput);

/// <summary>
/// The raw output of one encoder.
/// </summary>
/// <param name="Scores">
/// Post-sigmoid scores in the model's own output order. They are bounded to
/// [0, 1] but are not claimed to be calibrated probabilities.
/// </param>
public sealed record EncoderInferenceResult(
    string ModelId,
    string ModelVersion,
    IReadOnlyList<double> Scores,
    double DurationMilliseconds);

/// <summary>
/// The encoder capability is configured but the call failed.
/// </summary>
public sealed class EncoderInferenceException : Exception
{
    public EncoderInferenceException(string message)
        : base(message)
    {
    }

    public EncoderInferenceException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// The encoder capability cannot run at all.
/// </summary>
/// <remarks>
/// Distinct from <see cref="EncoderInferenceException"/> on purpose: an absent
/// runtime is an ordinary state of the product, a broken one is a defect, and
/// the two must not reach a reviewer as the same message.
/// </remarks>
public sealed class EncoderInferenceUnavailableException(string message)
    : Exception(message);

/// <summary>
/// Executes a resident encoder over a serialized input.
/// </summary>
/// <remarks>
/// Deliberately field-agnostic and technology-agnostic. It knows a model
/// identity and a string, and returns numbers. Genre/Form, the thresholds, the
/// cascade rule, the label mapping and the product taxonomy are decisions of
/// the inference plan above it, exactly as the V2.1 handoff requires.
///
/// The hosting mechanism — today a resident CPU worker — can be replaced
/// without changing this contract.
/// </remarks>
public interface IEncoderInferenceRuntime
{
    /// <summary>
    /// Gets whether the runtime can currently serve an inference.
    /// </summary>
    Task<bool> IsAvailableAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs one inference. Output is untrusted and validated by the caller.
    /// </summary>
    /// <remarks>
    /// Cancellation surfaces as <see cref="OperationCanceledException"/> and is
    /// never reported as a failure. No retry happens here.
    /// </remarks>
    Task<EncoderInferenceResult> InferAsync(
        EncoderInferenceRequest request,
        CancellationToken cancellationToken);
}
