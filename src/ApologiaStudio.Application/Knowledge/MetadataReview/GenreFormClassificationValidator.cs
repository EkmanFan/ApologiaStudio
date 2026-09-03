namespace ApologiaStudio.Application.Knowledge.MetadataReview;

/// <summary>
/// Deterministic validation of untrusted model output against the active
/// Genre/Form policy.
///
/// Fails closed: a single violation discards the whole classification rather
/// than salvaging the acceptable part, because a model that invented one term
/// gives no reason to trust the rest of the same response.
/// </summary>
public sealed class GenreFormClassificationValidator
    : IGenreFormClassificationValidator
{
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

        ValidateCardinality(suggested, policy, errors);
        ValidateDisjoint(suggested, rejected, errors);
        ValidateHierarchy(suggested, policy, errors);
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
            var term = Resolve(candidate.AuthorityId, policy, errors);
            if (term is null)
            {
                continue;
            }

            if (term.Usage != GenreFormPolicyUsage.Selectable)
            {
                errors.Add(new GenreFormValidationError(
                    GenreFormValidationFailure.TermNotSelectable,
                    $"'{term.PreferredLabel}' is structural in the active profile."));
                continue;
            }

            if (!seen.Add(term.AuthorityUri))
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
                term.AuthorityUri,
                term.AuthorityIdentifier,
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
            var term = Resolve(candidate.AuthorityId, policy, errors);
            if (term is null)
            {
                continue;
            }

            if (!seen.Add(term.AuthorityUri))
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
                term.AuthorityUri,
                term.PreferredLabel,
                candidate.Reason.Trim()));
        }

        return resolved;
    }

    /// <summary>
    /// Resolves an identifier the model produced. A term is accepted only when
    /// it matches the active policy by authority URI or authority identifier;
    /// a preferred label is never accepted, so an invented label can never be
    /// coerced into a real term.
    /// </summary>
    private static GenreFormPolicyTerm? Resolve(
        string? authorityId,
        GenreFormPolicySnapshot policy,
        List<GenreFormValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(authorityId))
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.MissingAuthorityId,
                "A returned entry carries no authority identifier."));
            return null;
        }

        var candidate = authorityId.Trim();

        var term = policy.Find(candidate) ??
                   policy.Terms.FirstOrDefault(
                       x => string.Equals(
                           x.AuthorityIdentifier,
                           candidate,
                           StringComparison.Ordinal));

        if (term is null)
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.UnknownAuthorityTerm,
                $"'{candidate}' is not a term of the active profile."));
        }

        return term;
    }

    private static void ValidateCardinality(
        List<GenreFormSuggestion> suggested,
        GenreFormPolicySnapshot policy,
        List<GenreFormValidationError> errors)
    {
        if (suggested.Count > policy.MaximumSuggestions)
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.TooManySuggestions,
                $"{suggested.Count} suggestions exceed the bound of " +
                $"{policy.MaximumSuggestions}."));
        }
    }

    private static void ValidateDisjoint(
        List<GenreFormSuggestion> suggested,
        List<GenreFormRejection> rejected,
        List<GenreFormValidationError> errors)
    {
        var suggestedUris = suggested
            .Select(x => x.AuthorityUri)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var rejection in rejected.Where(
                     x => suggestedUris.Contains(x.AuthorityUri)))
        {
            errors.Add(new GenreFormValidationError(
                GenreFormValidationFailure.SuggestedAndRejected,
                $"'{rejection.PreferredLabel}' is both suggested and rejected."));
        }
    }

    /// <summary>
    /// GF-RULE-08: on one ancestor path only the most specific applicable term
    /// is kept. Two unrelated genres may coexist.
    /// </summary>
    private static void ValidateHierarchy(
        List<GenreFormSuggestion> suggested,
        GenreFormPolicySnapshot policy,
        List<GenreFormValidationError> errors)
    {
        foreach (var candidate in suggested)
        {
            var term = policy.Find(candidate.AuthorityUri);
            if (term is null)
            {
                continue;
            }

            foreach (var other in suggested)
            {
                if (ReferenceEquals(candidate, other))
                {
                    continue;
                }

                if (term.AncestorAuthorityUris.Contains(
                        other.AuthorityUri,
                        StringComparer.Ordinal))
                {
                    errors.Add(new GenreFormValidationError(
                        GenreFormValidationFailure.RedundantHierarchy,
                        $"'{other.PreferredLabel}' is a broader term of " +
                        $"'{candidate.PreferredLabel}'; only the most specific " +
                        "applicable term may be suggested."));
                }
            }
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
