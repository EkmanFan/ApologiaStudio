using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.Application.Knowledge.MetadataReview;

/// <summary>
/// One Genre/Form analysis of an editorial draft, as the review panel sees it.
/// </summary>
/// <param name="Result">
/// Present for a run that produced an answer, including a run that answered
/// "no term applies". Absent when nothing ran.
/// </param>
public sealed record GenreFormFieldAnalysis(
    FieldSuggestionStatus Status,
    GenreFormClassificationResult? Result,
    string? FailureReason);

/// <summary>
/// Obtains Genre/Form suggestions for an editorial draft.
/// </summary>
/// <remarks>
/// The one place that turns a generic field suggestion into the Genre/Form
/// advisory record the review workflow already understands. It sits here rather
/// than in the panel so the mapping is testable without a UI, and so the panel
/// keeps no second copy of the evidence policy or the vocabulary.
///
/// Provider output stays untrusted: a value that is not an active,
/// encoder-predictable product term fails the whole analysis rather than being
/// dropped, because a provider that returned one impossible code gives no
/// reason to trust the others.
/// </remarks>
public sealed class GenreFormFieldSuggestionService(
    IFieldSuggestionProvider suggestions)
{
    #region Methods

    public async Task<GenreFormFieldAnalysis> AnalyzeAsync(
        DocumentManagerEditorialDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var batch = await suggestions.GetSuggestionsAsync(
            new FieldSuggestionRequest(
                draft.Id,
                EditorialDraftSuggestionEvidence.From(draft)),
            [ApologiaFieldId.GenreForm],
            cancellationToken);

        batch.EnsureAnswers([ApologiaFieldId.GenreForm]);

        var result = batch.Find(ApologiaFieldId.GenreForm)!;

        if (result.Status is FieldSuggestionStatus.Unavailable
            or FieldSuggestionStatus.Failed)
        {
            return new GenreFormFieldAnalysis(result.Status, null, null);
        }

        try
        {
            return new GenreFormFieldAnalysis(
                result.Status,
                Project(result),
                null);
        }
        catch (FieldSuggestionContractException exception)
        {
            // A provider that broke the contract is a defect, not an opinion.
            return new GenreFormFieldAnalysis(
                FieldSuggestionStatus.Failed,
                null,
                exception.Message);
        }
    }

    #endregion

    #region Methods Projection

    private static GenreFormClassificationResult Project(
        FieldSuggestionResult result)
    {
        var identity = Identity(result.Provenance);
        var projected = new List<GenreFormSuggestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var suggestion in result.Suggestions)
        {
            var term = ApologiaGenreFormTaxonomy.Find(suggestion.ValueId)
                       ?? throw new FieldSuggestionContractException(
                           $"'{suggestion.ValueId}' is not an Apologia " +
                           "Genre/Form product term.");

            if (term.PredictionMode == GenreFormPredictionMode.ManualOnly)
            {
                throw new FieldSuggestionContractException(
                    $"'{term.Code}' is assigned by a reviewer and can never " +
                    "be proposed by a machine.");
            }

            if (!seen.Add(term.Code))
            {
                throw new FieldSuggestionContractException(
                    $"'{term.Code}' was suggested more than once.");
            }

            projected.Add(
                new GenreFormSuggestion(
                    term.Code,
                    term.PreferredLabel,
                    // An encoder proposes; it does not argue.
                    Justification: null,
                    Evidence: [],
                    suggestion.Score));
        }

        return new GenreFormClassificationResult(
            identity,
            projected,
            ConsideredButRejected: [],
            InsufficientEvidence: false);
    }

    private static MetadataReviewAnalysisIdentity Identity(
        FieldSuggestionProvenance provenance)
    {
        if (string.IsNullOrWhiteSpace(provenance.ModelId))
        {
            throw new FieldSuggestionContractException(
                "An answered analysis carries no model identity.");
        }

        return new MetadataReviewAnalysisIdentity(
            ApologiaGenreFormTaxonomy.Version,
            provenance.Provider,
            provenance.ModelId,
            DateTimeOffset.UtcNow,
            PromptVersion: null,
            provenance.PlanId,
            provenance.ModelVersion);
    }

    #endregion
}
