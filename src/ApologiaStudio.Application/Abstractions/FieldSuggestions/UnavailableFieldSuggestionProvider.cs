namespace ApologiaStudio.Application.Abstractions.FieldSuggestions;

/// <summary>
/// The suggestion capability when no machine capability is configured.
/// </summary>
/// <remarks>
/// A Null Object rather than an absent registration. Consumers resolve one
/// <see cref="IFieldSuggestionProvider"/> and call it; the answer is
/// <see cref="FieldSuggestionStatus.Unavailable"/>, which is an ordinary
/// supported state of the product. Making the port optional instead would push
/// a null check into every future caller and invite each of them to invent its
/// own meaning for "no assistance".
///
/// Missing machine assistance is never an exception here: Apologia must start,
/// review metadata by hand and save authoritatively with no model runtime at
/// all. What this type must never do is disguise a real execution failure as
/// unavailability — it runs nothing, so it can fail at nothing.
/// </remarks>
public sealed class UnavailableFieldSuggestionProvider : IFieldSuggestionProvider
{
    #region Variables and Constants

    /// <summary>
    /// Provenance identity of the absent capability.
    /// </summary>
    public const string ProviderId = "unavailable";

    private static readonly FieldSuggestionProvenance Provenance = new(ProviderId);

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task<FieldSuggestionBatch> GetSuggestionsAsync(
        FieldSuggestionRequest request,
        IReadOnlyCollection<ApologiaFieldId>? fields = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Cancellation stays cancellation, even on a provider that does no work.
        cancellationToken.ThrowIfCancellationRequested();

        if (fields is null)
        {
            // This provider supports no field, so the set of fields it can
            // evaluate on its own initiative is empty. It answers nothing
            // rather than claiming every conceivable field is unavailable.
            return Task.FromResult(FieldSuggestionBatch.Empty);
        }

        var answered = new List<FieldSuggestionResult>();
        var seen = new HashSet<ApologiaFieldId>();

        foreach (var fieldId in fields)
        {
            if (fieldId.Value is null)
            {
                throw new FieldSuggestionContractException(
                    "A requested field carries no identity.");
            }

            // A field asked for twice is one field, and the batch answers each
            // field once.
            if (seen.Add(fieldId))
            {
                answered.Add(
                    FieldSuggestionResult.Unavailable(fieldId, Provenance));
            }
        }

        return Task.FromResult(new FieldSuggestionBatch(answered));
    }

    #endregion
}
