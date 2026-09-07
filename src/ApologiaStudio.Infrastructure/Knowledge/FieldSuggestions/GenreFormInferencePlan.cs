using ApologiaStudio.Application.Abstractions.FieldSuggestions;

namespace ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

/// <summary>
/// The frozen Spike Encoder V2.1 cascade for the Genre/Form field.
/// </summary>
/// <remarks>
/// Owns every product decision the handoff assigns to the application:
/// serialization, model identity, thresholds, label mapping, fallback
/// orchestration, validation and provenance. The runtime beneath it owns none
/// of them.
///
/// The fallback is <b>semantic, not a high-availability mechanism</b>. It exists
/// because a successful primary run that proposes nothing is a case mDeBERTa
/// rescues; it does not exist to paper over a broken primary. A primary that
/// throws therefore never reaches it: substituting a second model for a failed
/// first one would silently change which model is answering, and the reviewer
/// would never know.
/// </remarks>
public sealed class GenreFormInferencePlan(
    IEncoderInferenceRuntime runtime)
{
    #region Variables and Constants

    /// <summary>
    /// Identity of the frozen cascade, recorded in provenance.
    /// </summary>
    public const string PlanId = "genre-form-cascade/v2.1";

    public const string PrimaryModelId = "xlm-roberta-large";

    public const double PrimaryThreshold = 0.47;

    public const string FallbackModelId = "mdeberta-v3-base";

    public const double FallbackThreshold = 0.43;

    #endregion

    #region Methods

    /// <summary>
    /// Runs the cascade for one document.
    /// </summary>
    public async Task<FieldSuggestionResult> RunAsync(
        string provider,
        FieldSuggestionEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentNullException.ThrowIfNull(evidence);

        var serialized = GenreFormEncoderSerializer.Serialize(evidence);

        if (serialized.Length == 0)
        {
            // Nothing to classify. Answering NoSuggestion would claim the
            // encoder looked and formed a judgement; it never ran.
            return FieldSuggestionResult.Unavailable(
                ApologiaFieldId.GenreForm,
                Provenance(provider, null));
        }

        if (!await runtime.IsAvailableAsync(cancellationToken))
        {
            return FieldSuggestionResult.Unavailable(
                ApologiaFieldId.GenreForm,
                Provenance(provider, null));
        }

        EncoderInferenceResult primary;

        try
        {
            primary = await RunModelAsync(
                PrimaryModelId,
                serialized,
                cancellationToken);
        }
        catch (EncoderInferenceUnavailableException)
        {
            return FieldSuggestionResult.Unavailable(
                ApologiaFieldId.GenreForm,
                Provenance(provider, null));
        }
        catch (EncoderInferenceException)
        {
            // A technical failure of the primary never falls back.
            return FieldSuggestionResult.Failed(
                ApologiaFieldId.GenreForm,
                Provenance(provider, PrimaryModelId));
        }

        var primarySuggestions = Select(primary, PrimaryThreshold);

        if (primarySuggestions.Count > 0)
        {
            return FieldSuggestionResult.Succeeded(
                ApologiaFieldId.GenreForm,
                primarySuggestions,
                Provenance(provider, primary.ModelId, primary.ModelVersion));
        }

        // The primary ran and proposed nothing: the one case the fallback
        // exists for.
        EncoderInferenceResult fallback;

        try
        {
            fallback = await RunModelAsync(
                FallbackModelId,
                serialized,
                cancellationToken);
        }
        catch (EncoderInferenceUnavailableException)
        {
            return FieldSuggestionResult.Unavailable(
                ApologiaFieldId.GenreForm,
                Provenance(provider, FallbackModelId));
        }
        catch (EncoderInferenceException)
        {
            return FieldSuggestionResult.Failed(
                ApologiaFieldId.GenreForm,
                Provenance(provider, FallbackModelId));
        }

        var fallbackSuggestions = Select(fallback, FallbackThreshold);

        return fallbackSuggestions.Count > 0
            ? FieldSuggestionResult.Succeeded(
                ApologiaFieldId.GenreForm,
                fallbackSuggestions,
                Provenance(provider, fallback.ModelId, fallback.ModelVersion))
            : FieldSuggestionResult.NoSuggestion(
                ApologiaFieldId.GenreForm,
                Provenance(provider, fallback.ModelId, fallback.ModelVersion));
    }

    #endregion

    #region Methods Execution

    private async Task<EncoderInferenceResult> RunModelAsync(
        string modelId,
        string serializedInput,
        CancellationToken cancellationToken)
    {
        var result = await runtime.InferAsync(
            new EncoderInferenceRequest(modelId, serializedInput),
            cancellationToken);

        Validate(modelId, result);

        return result;
    }

    /// <summary>
    /// Refuses a runtime response the contract forbids.
    /// </summary>
    /// <remarks>
    /// Model output is untrusted. A wrong output count means the artifact is
    /// not the one this plan was written against, and a non-finite score means
    /// the numbers cannot be compared to a threshold at all — both are failures,
    /// never a quiet absence of suggestions.
    /// </remarks>
    private static void Validate(
        string expectedModelId,
        EncoderInferenceResult result)
    {
        if (!string.Equals(result.ModelId, expectedModelId, StringComparison.Ordinal))
        {
            throw new EncoderInferenceException(
                $"Expected model '{expectedModelId}' but the runtime answered " +
                $"as '{result.ModelId}'.");
        }

        if (string.IsNullOrWhiteSpace(result.ModelVersion))
        {
            throw new EncoderInferenceException(
                $"Model '{expectedModelId}' returned no artifact version.");
        }

        if (result.Scores.Count != GenreFormEncoderLabelMap.OutputCount)
        {
            throw new EncoderInferenceException(
                $"Model '{expectedModelId}' returned {result.Scores.Count} " +
                $"outputs, expected {GenreFormEncoderLabelMap.OutputCount}.");
        }

        for (var index = 0; index < result.Scores.Count; index++)
        {
            var score = result.Scores[index];

            if (double.IsNaN(score) || double.IsInfinity(score))
            {
                throw new EncoderInferenceException(
                    $"Model '{expectedModelId}' returned a non-finite score " +
                    $"at output {index}.");
            }

            if (score is < 0 or > 1)
            {
                throw new EncoderInferenceException(
                    $"Model '{expectedModelId}' returned {score} at output " +
                    $"{index}, outside the post-sigmoid range [0, 1].");
            }
        }
    }

    /// <summary>
    /// Keeps the outputs at or above the threshold, most confident first.
    /// </summary>
    private static IReadOnlyList<FieldSuggestion> Select(
        EncoderInferenceResult result,
        double threshold)
    {
        var selected = new List<FieldSuggestion>();

        for (var index = 0; index < result.Scores.Count; index++)
        {
            if (result.Scores[index] >= threshold)
            {
                selected.Add(
                    new FieldSuggestion(
                        GenreFormEncoderLabelMap.CodeAt(index),
                        result.Scores[index]));
            }
        }

        return selected
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.ValueId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Provenance of the decision that was actually returned.
    /// </summary>
    /// <remarks>
    /// The cascade can run twice, and the model identity alone says which path
    /// was taken: a result attributed to <c>mdeberta-v3-base</c> can only have
    /// been produced after the primary ran and proposed nothing. No separate
    /// trace model is needed to know whether the fallback ran.
    /// </remarks>
    private static FieldSuggestionProvenance Provenance(
        string provider,
        string? modelId,
        string? modelVersion = null) =>
        new(provider, PlanId, modelId, modelVersion);

    #endregion
}
