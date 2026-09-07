namespace ApologiaStudio.Application.Abstractions.FieldSuggestions;

/// <summary>
/// Raised when a suggestion provider returns a response the contract forbids.
/// </summary>
/// <remarks>
/// Provider output is untrusted, exactly like model output elsewhere in the
/// application. An incoherent response is a defect to surface, never something
/// to repair into a plausible-looking suggestion.
/// </remarks>
public sealed class FieldSuggestionContractException(string message)
    : Exception(message);

/// <summary>
/// Outcome of one field's suggestion attempt.
/// </summary>
/// <remarks>
/// The three negatives are never interchangeable. A reviewer told "no term
/// applies" is being given a judgement; a reviewer told "nothing ran" is being
/// told to decide alone; a reviewer told "it broke" is being told something is
/// wrong. Collapsing them would silently turn an outage into an opinion.
/// </remarks>
public enum FieldSuggestionStatus
{
    /// <summary>The capability ran and proposes at least one value.</summary>
    Succeeded = 0,

    /// <summary>
    /// The capability ran and concluded that no value meets its decision
    /// policy. A semantic result, not a failure.
    /// </summary>
    NoSuggestion = 1,

    /// <summary>
    /// The capability cannot run: none is configured, or its runtime or model
    /// is absent. Not a negative prediction.
    /// </summary>
    Unavailable = 2,

    /// <summary>The capability was available and its execution failed.</summary>
    Failed = 3
}

/// <summary>
/// Bounded evidence offered to the capability for one document.
/// </summary>
/// <remarks>
/// V1 carries what Apologia actually holds after P2-01. No document handle, no
/// DPEngine type, no database entity and no encoder-shaped section list: the
/// serialisation a trained model expects is the concern of whoever runs it.
///
/// Every value here is untrusted document content. It never changes the allowed
/// fields, the contract or application behaviour.
/// </remarks>
public sealed record FieldSuggestionEvidence(
    string? Title,
    string? Description)
{
    public static FieldSuggestionEvidence Empty { get; } = new(null, null);

    /// <summary>
    /// Gets whether the evidence carries nothing usable.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Title) &&
        string.IsNullOrWhiteSpace(Description);
}

/// <summary>
/// One suggestion request for one document.
/// </summary>
public sealed record FieldSuggestionRequest(
    Guid DocumentId,
    FieldSuggestionEvidence Evidence);

/// <summary>
/// How one field result was produced.
/// </summary>
/// <remarks>
/// Enough to attribute a suggestion to what made it, and no more. It carries no
/// reasoning, no prompt and no document content: provenance answers "which
/// capability said this", never "why it thinks so".
///
/// Only <see cref="Provider"/> has a producer in V1; the rest stay null until
/// a real model plan fills them.
/// </remarks>
public sealed record FieldSuggestionProvenance
{
    public FieldSuggestionProvenance(
        string provider,
        string? planId = null,
        string? modelId = null,
        string? modelVersion = null,
        string? headId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        Provider = provider;
        PlanId = planId;
        ModelId = modelId;
        ModelVersion = modelVersion;
        HeadId = headId;
    }

    public string Provider { get; }

    public string? PlanId { get; }

    public string? ModelId { get; }

    public string? ModelVersion { get; }

    public string? HeadId { get; }
}

/// <summary>
/// One proposed value for a field.
/// </summary>
/// <remarks>
/// <c>ValueId</c> is the canonical application identity of the value. For
/// Genre/Form this is the Apologia product term code — never an LCGFT URI, an
/// authority identity or a model output index.
///
/// <c>Score</c> is a confidence in [0, 1]. A bare number, deliberately: the
/// application does not claim it is a calibrated probability.
/// </remarks>
public sealed record FieldSuggestion
{
    public FieldSuggestion(
        string valueId,
        double score)
    {
        if (string.IsNullOrWhiteSpace(valueId) ||
            !string.Equals(valueId, valueId.Trim(), StringComparison.Ordinal))
        {
            throw new FieldSuggestionContractException(
                $"'{valueId}' is not a usable suggestion value identity.");
        }

        if (double.IsNaN(score) || score is < 0 or > 1)
        {
            throw new FieldSuggestionContractException(
                $"Suggestion '{valueId}' carries a score of {score}, outside " +
                "the accepted range [0, 1].");
        }

        ValueId = valueId;
        Score = score;
    }

    public string ValueId { get; }

    public double Score { get; }
}

/// <summary>
/// The outcome for one requested field.
/// </summary>
/// <remarks>
/// The invariants are enforced here rather than left to each caller, because a
/// provider is exactly the component that must not be trusted to respect them.
/// </remarks>
public sealed record FieldSuggestionResult
{
    #region Constructors

    public FieldSuggestionResult(
        ApologiaFieldId fieldId,
        FieldSuggestionStatus status,
        IReadOnlyList<FieldSuggestion> suggestions,
        FieldSuggestionProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        ArgumentNullException.ThrowIfNull(provenance);

        if (fieldId.Value is null)
        {
            throw new FieldSuggestionContractException(
                "A field result carries no field identity.");
        }

        if (status == FieldSuggestionStatus.Succeeded && suggestions.Count == 0)
        {
            throw new FieldSuggestionContractException(
                $"Field '{fieldId}' reports success with no suggestion. " +
                "An empty successful run is NoSuggestion.");
        }

        if (status != FieldSuggestionStatus.Succeeded && suggestions.Count > 0)
        {
            throw new FieldSuggestionContractException(
                $"Field '{fieldId}' reports '{status}' while carrying " +
                $"{suggestions.Count} suggestion(s).");
        }

        var duplicate = suggestions
            .GroupBy(x => x.ValueId, StringComparer.Ordinal)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicate is not null)
        {
            throw new FieldSuggestionContractException(
                $"Field '{fieldId}' suggests '{duplicate.Key}' more than once.");
        }

        FieldId = fieldId;
        Status = status;
        Suggestions = suggestions;
        Provenance = provenance;
    }

    #endregion

    #region Properties

    public ApologiaFieldId FieldId { get; }

    public FieldSuggestionStatus Status { get; }

    public IReadOnlyList<FieldSuggestion> Suggestions { get; }

    public FieldSuggestionProvenance Provenance { get; }

    #endregion

    #region Methods

    public static FieldSuggestionResult Succeeded(
        ApologiaFieldId fieldId,
        IReadOnlyList<FieldSuggestion> suggestions,
        FieldSuggestionProvenance provenance) =>
        new(fieldId, FieldSuggestionStatus.Succeeded, suggestions, provenance);

    public static FieldSuggestionResult NoSuggestion(
        ApologiaFieldId fieldId,
        FieldSuggestionProvenance provenance) =>
        new(fieldId, FieldSuggestionStatus.NoSuggestion, [], provenance);

    public static FieldSuggestionResult Unavailable(
        ApologiaFieldId fieldId,
        FieldSuggestionProvenance provenance) =>
        new(fieldId, FieldSuggestionStatus.Unavailable, [], provenance);

    public static FieldSuggestionResult Failed(
        ApologiaFieldId fieldId,
        FieldSuggestionProvenance provenance) =>
        new(fieldId, FieldSuggestionStatus.Failed, [], provenance);

    #endregion
}

/// <summary>
/// The outcome of one suggestion call, one result per evaluated field.
/// </summary>
public sealed record FieldSuggestionBatch
{
    #region Constructors

    public FieldSuggestionBatch(
        IReadOnlyList<FieldSuggestionResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var duplicate = results
            .GroupBy(x => x.FieldId)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicate is not null)
        {
            throw new FieldSuggestionContractException(
                $"Field '{duplicate.Key}' is answered more than once.");
        }

        Results = results;
    }

    #endregion

    #region Properties

    public static FieldSuggestionBatch Empty { get; } = new([]);

    public IReadOnlyList<FieldSuggestionResult> Results { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Finds the result for one field, or null when the field was not
    /// evaluated.
    /// </summary>
    public FieldSuggestionResult? Find(ApologiaFieldId fieldId) =>
        Results.FirstOrDefault(x => x.FieldId == fieldId);

    /// <summary>
    /// Verifies that every explicitly requested field was answered.
    /// </summary>
    /// <remarks>
    /// A field a caller asked about must come back with a status, even when the
    /// answer is only <see cref="FieldSuggestionStatus.Unavailable"/>. Omitting
    /// it reads as "nothing to say", which is a different claim.
    /// </remarks>
    public void EnsureAnswers(
        IReadOnlyCollection<ApologiaFieldId>? requested)
    {
        if (requested is null)
        {
            return;
        }

        foreach (var fieldId in requested)
        {
            if (Find(fieldId) is null)
            {
                throw new FieldSuggestionContractException(
                    $"Field '{fieldId}' was requested but not answered.");
            }
        }
    }

    #endregion
}

/// <summary>
/// Machine assistance for Apologia metadata fields.
/// </summary>
/// <remarks>
/// One port for every assisted field, rather than one interface per field: a
/// new field is a new value of <see cref="ApologiaFieldId"/>, not a new
/// abstraction.
///
/// The capability is optional. Exactly one implementation is registered, and
/// when no machine capability is configured that implementation answers
/// <see cref="FieldSuggestionStatus.Unavailable"/>. Consumers therefore never
/// null-check it, and Apologia never requires a model runtime to edit metadata.
/// </remarks>
public interface IFieldSuggestionProvider
{
    /// <summary>
    /// Requests suggestions for a document.
    /// </summary>
    /// <param name="fields">
    /// The fields to evaluate. Null evaluates every field the active provider
    /// supports. When supplied, every listed field is answered, including one
    /// the provider does not support.
    /// </param>
    /// <remarks>
    /// Cancellation stays cancellation: it surfaces as an
    /// <see cref="OperationCanceledException"/> and never becomes
    /// <see cref="FieldSuggestionStatus.Failed"/>. No retry happens here.
    /// </remarks>
    Task<FieldSuggestionBatch> GetSuggestionsAsync(
        FieldSuggestionRequest request,
        IReadOnlyCollection<ApologiaFieldId>? fields = null,
        CancellationToken cancellationToken = default);
}
