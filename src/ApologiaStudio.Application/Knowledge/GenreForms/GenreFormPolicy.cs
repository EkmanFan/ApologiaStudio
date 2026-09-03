namespace ApologiaStudio.Application.Knowledge.GenreForms;

/// <summary>
/// Usage of a term inside the active profile. Only <see cref="Selectable"/>
/// terms may be chosen; structural ancestors are carried so hierarchy
/// redundancy can be detected without a database round trip.
/// </summary>
public enum GenreFormPolicyUsage
{
    StructuralOnly = 0,
    Selectable = 1
}

public sealed record GenreFormPolicyTerm(
    string AuthorityUri,
    string AuthorityIdentifier,
    string PreferredLabel,
    GenreFormPolicyUsage Usage,
    IReadOnlyList<string> AncestorAuthorityUris);

/// <summary>
/// The active Genre/Form policy captured as a value, so vocabulary rules can
/// be applied without persistence. Built from the Knowledge Store in
/// production and from a fixture in evaluation.
///
/// Consumed both by editorial review, which records a reviewer's selection,
/// and by the metadata assistant, which proposes one.
/// </summary>
public sealed record GenreFormPolicySnapshot(
    string PolicyVersion,
    IReadOnlyList<GenreFormPolicyTerm> Terms)
{
    public IEnumerable<GenreFormPolicyTerm> SelectableTerms =>
        Terms.Where(x => x.Usage == GenreFormPolicyUsage.Selectable);

    public GenreFormPolicyTerm? Find(string authorityUri)
    {
        return Terms.FirstOrDefault(
            x => string.Equals(x.AuthorityUri, authorityUri, StringComparison.Ordinal));
    }
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
    NotSelectable = 1,
    Duplicate = 2,
    RedundantHierarchy = 3
}

public sealed record GenreFormSelectionError(
    GenreFormSelectionFailure Failure,
    string AuthorityId,
    string Detail);

/// <summary>
/// The single implementation of the Genre/Form selection rules.
///
/// A reviewer choosing terms by hand and an assistant proposing them are
/// judged by exactly the same rules; neither the UI nor the assistant restates
/// the vocabulary or the hierarchy.
/// </summary>
public static class GenreFormSelectionRules
{
    /// <summary>
    /// Resolves an identifier by authority URI or authority identifier only.
    /// A preferred label is never accepted, so an invented label cannot be
    /// coerced into a real term.
    /// </summary>
    public static GenreFormPolicyTerm? Resolve(
        string? authorityId,
        GenreFormPolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (string.IsNullOrWhiteSpace(authorityId))
        {
            return null;
        }

        var candidate = authorityId.Trim();

        return policy.Find(candidate) ??
               policy.Terms.FirstOrDefault(
                   x => string.Equals(
                       x.AuthorityIdentifier,
                       candidate,
                       StringComparison.Ordinal));
    }

    /// <summary>
    /// Validates a complete selection. An empty selection is valid: a work may
    /// legitimately carry no genre/form.
    /// </summary>
    public static IReadOnlyList<GenreFormSelectionError> Validate(
        IReadOnlyList<string> authorityIds,
        GenreFormPolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(authorityIds);
        ArgumentNullException.ThrowIfNull(policy);

        var errors = new List<GenreFormSelectionError>();
        var resolved = new List<GenreFormPolicyTerm>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var authorityId in authorityIds)
        {
            var term = Resolve(authorityId, policy);

            if (term is null)
            {
                errors.Add(new GenreFormSelectionError(
                    GenreFormSelectionFailure.UnknownTerm,
                    authorityId ?? string.Empty,
                    "The term does not belong to the active profile."));
                continue;
            }

            if (term.Usage != GenreFormPolicyUsage.Selectable)
            {
                errors.Add(new GenreFormSelectionError(
                    GenreFormSelectionFailure.NotSelectable,
                    term.AuthorityUri,
                    $"'{term.PreferredLabel}' is structural and cannot be assigned."));
                continue;
            }

            if (!seen.Add(term.AuthorityUri))
            {
                errors.Add(new GenreFormSelectionError(
                    GenreFormSelectionFailure.Duplicate,
                    term.AuthorityUri,
                    $"'{term.PreferredLabel}' appears more than once."));
                continue;
            }

            resolved.Add(term);
        }

        errors.AddRange(FindRedundantHierarchy(resolved));

        return errors;
    }

    /// <summary>
    /// On one ancestor path only the most specific applicable term is kept.
    /// Two unrelated genres may coexist.
    /// </summary>
    public static IReadOnlyList<GenreFormSelectionError> FindRedundantHierarchy(
        IReadOnlyList<GenreFormPolicyTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var errors = new List<GenreFormSelectionError>();

        foreach (var candidate in terms)
        {
            foreach (var other in terms)
            {
                if (ReferenceEquals(candidate, other))
                {
                    continue;
                }

                if (candidate.AncestorAuthorityUris.Contains(
                        other.AuthorityUri,
                        StringComparer.Ordinal))
                {
                    errors.Add(new GenreFormSelectionError(
                        GenreFormSelectionFailure.RedundantHierarchy,
                        other.AuthorityUri,
                        $"'{other.PreferredLabel}' is a broader term of " +
                        $"'{candidate.PreferredLabel}'; keep only the most " +
                        "specific applicable term."));
                }
            }
        }

        return errors;
    }
}
