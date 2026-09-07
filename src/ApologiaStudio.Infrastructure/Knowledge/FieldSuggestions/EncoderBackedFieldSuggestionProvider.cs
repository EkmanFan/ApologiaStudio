using ApologiaStudio.Application.Abstractions.FieldSuggestions;

namespace ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

/// <summary>
/// The field-suggestion capability backed by the resident CPU encoders.
/// </summary>
/// <remarks>
/// Dispatch is one explicit branch: <c>genre_form</c> runs the Genre/Form plan
/// and every other field is answered <see cref="FieldSuggestionStatus.Unavailable"/>.
/// A registry, a keyed service or reflection over field ids would buy nothing
/// today and hide the mapping the moment a second field arrives.
/// </remarks>
public sealed class EncoderBackedFieldSuggestionProvider(
    GenreFormInferencePlan genreFormPlan)
    : IFieldSuggestionProvider
{
    #region Variables and Constants

    /// <summary>
    /// Provenance identity of this capability.
    /// </summary>
    public const string ProviderId = "encoder";

    /// <summary>
    /// The fields this provider evaluates on its own initiative.
    /// </summary>
    private static readonly ApologiaFieldId[] SupportedFields =
        [ApologiaFieldId.GenreForm];

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<FieldSuggestionBatch> GetSuggestionsAsync(
        FieldSuggestionRequest request,
        IReadOnlyCollection<ApologiaFieldId>? fields = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var requested = fields ?? SupportedFields;
        var results = new List<FieldSuggestionResult>();
        var seen = new HashSet<ApologiaFieldId>();

        foreach (var fieldId in requested)
        {
            if (fieldId.Value is null)
            {
                throw new FieldSuggestionContractException(
                    "A requested field carries no identity.");
            }

            if (!seen.Add(fieldId))
            {
                continue;
            }

            results.Add(
                fieldId == ApologiaFieldId.GenreForm
                    ? await genreFormPlan.RunAsync(
                        ProviderId,
                        request.Evidence,
                        cancellationToken)
                    : FieldSuggestionResult.Unavailable(
                        fieldId,
                        new FieldSuggestionProvenance(ProviderId)));
        }

        return new FieldSuggestionBatch(results);
    }

    #endregion
}
