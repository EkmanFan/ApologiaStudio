namespace ApologiaStudio.Application.Knowledge.GenreForms;

/// <summary>
/// One product term as seen by the selection and validation rules.
/// </summary>
/// <remarks>
/// Identity is <see cref="Code"/>. <see cref="PredictionMode"/> is carried for
/// display and for the future encoder scope; it is not a selection rule, and a
/// reviewer may assign a manual-only term like any other.
/// </remarks>
public sealed record GenreFormPolicyTerm(
    string Code,
    string PreferredLabel,
    GenreFormPredictionMode PredictionMode);

/// <summary>
/// The active Genre/Form policy captured as a value, so vocabulary rules can
/// be applied without persistence.
/// </summary>
/// <remarks>
/// The policy is the Apologia product taxonomy. It states what it actually
/// governs — a flat, closed set of product concepts — rather than an external
/// authority profile: LCGFT is an alignment target, not the vocabulary a
/// reviewer or an assistant chooses from.
///
/// Consumed both by editorial review, which records a reviewer's selection,
/// and by the metadata assistant, which proposes one.
/// </remarks>
public sealed record GenreFormPolicySnapshot(
    string TaxonomyVersion,
    IReadOnlyList<GenreFormPolicyTerm> Terms)
{
    /// <summary>
    /// Gets the terms the encoder will be allowed to predict.
    /// </summary>
    public IEnumerable<GenreFormPolicyTerm> EncoderPredictableTerms =>
        Terms.Where(
            x => x.PredictionMode == GenreFormPredictionMode.EncoderPredictable);

    /// <summary>
    /// Finds a term by its canonical product code.
    /// </summary>
    public GenreFormPolicyTerm? Find(string? code) =>
        code is null
            ? null
            : Terms.FirstOrDefault(
                x => string.Equals(x.Code, code, StringComparison.Ordinal));
}

/// <summary>
/// Supplies the active policy. Kept as a port so vocabulary rules never depend
/// on persistence.
/// </summary>
public interface IGenreFormPolicyProvider
{
    Task<GenreFormPolicySnapshot> GetActivePolicyAsync(
        CancellationToken cancellationToken);
}

public enum GenreFormSelectionFailure
{
    UnknownTerm = 0,
    Duplicate = 1
}

public sealed record GenreFormSelectionError(
    GenreFormSelectionFailure Failure,
    string TermCode,
    string Detail);

/// <summary>
/// The single implementation of the Genre/Form selection rules.
/// </summary>
/// <remarks>
/// A reviewer choosing terms by hand and an assistant proposing them are
/// judged by exactly the same rules; neither the UI nor the assistant restates
/// the vocabulary.
///
/// V1 has two rules and no more. The taxonomy is flat, so there is no ancestor
/// path on which a selection could be redundant, and every active term is
/// selectable, so there is no structural tier to exclude.
/// </remarks>
public static class GenreFormSelectionRules
{
    /// <summary>
    /// Resolves a term by its canonical product code only. A preferred label is
    /// never accepted, so an invented label cannot be coerced into a real term.
    /// </summary>
    public static GenreFormPolicyTerm? Resolve(
        string? termCode,
        GenreFormPolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return string.IsNullOrWhiteSpace(termCode)
            ? null
            : policy.Find(termCode.Trim());
    }

    /// <summary>
    /// Validates a complete selection. An empty selection is valid: a work may
    /// legitimately carry no genre/form. Several terms are valid too.
    /// </summary>
    public static IReadOnlyList<GenreFormSelectionError> Validate(
        IReadOnlyList<string> termCodes,
        GenreFormPolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(termCodes);
        ArgumentNullException.ThrowIfNull(policy);

        var errors = new List<GenreFormSelectionError>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var termCode in termCodes)
        {
            var term = Resolve(termCode, policy);

            if (term is null)
            {
                errors.Add(new GenreFormSelectionError(
                    GenreFormSelectionFailure.UnknownTerm,
                    termCode ?? string.Empty,
                    "The term does not belong to the active taxonomy."));
                continue;
            }

            if (!seen.Add(term.Code))
            {
                errors.Add(new GenreFormSelectionError(
                    GenreFormSelectionFailure.Duplicate,
                    term.Code,
                    $"'{term.PreferredLabel}' appears more than once."));
            }
        }

        return errors;
    }
}
