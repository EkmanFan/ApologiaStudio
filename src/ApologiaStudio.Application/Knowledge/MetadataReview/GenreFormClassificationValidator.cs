using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.Application.Knowledge.MetadataReview;

/// <summary>
/// Deterministic validation of untrusted model output against the active
/// Genre/Form product taxonomy.
///
/// Fails closed: a single violation discards the whole classification rather
/// than salvaging the acceptable part, because a model that invented one term
/// gives no reason to trust the rest of the same response.
///
/// V1 checks what the flat product taxonomy can actually be violated on: the
/// term is known and active, it appears once, it carries a justification, and
/// the answer does not contradict itself. There is no structural tier and no
/// hierarchy, so there is nothing there left to check.
/// </summary>
public sealed class GenreFormClassificationValidator(
    MetadataReviewOptions? options = null)
    : IGenreFormClassificationValidator
{
    private readonly MetadataReviewOptions _options =
        options ?? MetadataReviewOptions.Default;

    public GenreFormClassificationValidation Validate(
        RawGenreFormClassification raw,
        GenreFormPolicySnapshot policy,
        MetadataReviewAnalysisIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(identity);

        var errors = new List<GenreFormValidationError>();

        var suggested = ResolveSuggestions(raw, policy, errors);
        var rejected = ResolveRejections(raw, policy, errors);

        ValidateCardinality(suggested, _options, errors);
        ValidateDisjoint(suggested, rejected, errors);
        ValidateInsufficientEvidence(raw, suggested, errors);

        if (errors.Count > 0)
        {
            return new GenreFormClassificationValidation(false, null, errors);
        }

        return new GenreFormClassificationValidation(
            true,
            new GenreFormClassificationResult(
                identity,
                suggested,
                rejected,
                raw.InsufficientEvidence),
            []);
    }

    private static List<GenreFormSuggestion> ResolveSuggestions(
        RawGenreFormClassification raw,
        GenreFormPolicySnapshot policy,
        List<GenreFormValidationError> errors)
    {
        var resolved = new List<GenreFormSuggestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in raw.Suggested)
        {
            var term = Resolve(candidate.TermCode, policy, errors);
            if (term is null)
            {
                continue;
            }

            if (!seen.Add(term.Code))
            {
                errors.Add(new GenreFormValidationError(
                    GenreFormValidationFailure.DuplicateSuggestion,
                    $"'{term.PreferredLabel}' was suggested more than once."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(candidate.Justification))
            {
                errors.Add(new GenreFormValidationError(
                    GenreFormValidationFailure.MissingJustification,
                    $"'{term.PreferredLabel}' carries no justification."));
                continue;
            }

            resolved.Add(new GenreFormSuggestion(
                term.Code,
                term.PreferredLabel,
                candidate.Justification.Trim(),
                candidate.Evidence
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList()));
        }

        return resolved;
    }

    private static List<GenreFormRejection> ResolveRejections(
        RawGenreFormClassification raw,
        GenreFormPolicySnapshot policy,
        List<GenreFormValidationError> errors)
    {
        var resolved = new List<GenreFormRejection>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in raw.ConsideredButRejected)
        {
            var term = Resolve(candidate.TermCode, policy, errors);
            if (term is null)
            {
                continue;
            }

            if (!seen.Add(term.Code))
            {
                errors.Add(new GenreFormValidationError(
                    GenreFormValidationFailure.DuplicateSuggestion,
                    $"'{term.PreferredLabel}' was rejected more than once."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(candidate.Reason))
            {
                errors.Add(new GenreFormValidationError(
                    GenreFormValidationFailure.MissingRejectionReason,
                    $"'{term.PreferredLabel}' was rejected without a reason."));
                continue;
            }

            resolved.Add(new GenreFormRejection(
                term.Code,
                term.PreferredLabel,
                candidate.Reason.Trim()));
        }

        return resolved;
    }

    /// <summary>
    /// Delegates to the shared selection rules so the assistant is judged by
    /// exactly the vocabulary rules a reviewer is.
    /// </summary>
    private static GenreFormPolicyTerm? Resolve(
        string? termCode,
        GenreFormPolicySnapshot policy,
        List<GenreFormValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(termCode))
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.MissingTermCode,
                "A returned entry carries no term code."));
            return null;
        }

        var term = GenreFormSelectionRules.Resolve(termCode, policy);

        if (term is null)
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.UnknownTerm,
                $"'{termCode.Trim()}' is not a term of the active taxonomy."));
        }

        return term;
    }

    private static void ValidateCardinality(
        List<GenreFormSuggestion> suggested,
        MetadataReviewOptions options,
        List<GenreFormValidationError> errors)
    {
        if (suggested.Count > options.MaximumSuggestions)
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.TooManySuggestions,
                $"{suggested.Count} suggestions exceed the assistant bound of " +
                $"{options.MaximumSuggestions}."));
        }
    }

    private static void ValidateDisjoint(
        List<GenreFormSuggestion> suggested,
        List<GenreFormRejection> rejected,
        List<GenreFormValidationError> errors)
    {
        var suggestedCodes = suggested
            .Select(x => x.Code)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var rejection in rejected.Where(
                     x => suggestedCodes.Contains(x.Code)))
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.SuggestedAndRejected,
                $"'{rejection.PreferredLabel}' is both suggested and rejected."));
        }
    }

    private static void ValidateInsufficientEvidence(
        RawGenreFormClassification raw,
        List<GenreFormSuggestion> suggested,
        List<GenreFormValidationError> errors)
    {
        if (raw.InsufficientEvidence && suggested.Count > 0)
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.ContradictoryInsufficientEvidence,
                "Evidence was declared insufficient while terms were suggested."));
        }
    }
}
