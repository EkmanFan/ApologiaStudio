namespace ApologiaStudio.Application.Knowledge.MetadataReview;

/// <summary>
/// Bounded evidence prepared for one classification. Selection is explicit and
/// testable: no component may quietly widen it to the whole document.
///
/// Every text field here is untrusted document content. It never alters the
/// allowed vocabulary, the rules, the schema or application behaviour.
/// </summary>
public sealed record MetadataReviewEvidence(
    string? Title,
    string? Subtitle,
    IReadOnlyList<string> Contributors,
    string? LanguageCode,
    string? EditionStatement,
    int? PublicationYear,
    string? PublicationPlace,
    string? Description,
    IReadOnlyList<MetadataReviewEvidenceSection> Sections)
{
    public static MetadataReviewEvidence Empty { get; } =
        new(null, null, [], null, null, null, null, null, []);
}

/// <summary>
/// One bounded excerpt. <paramref name="Reference"/> should point at a stable
/// page, section or document-element identifier so a reviewer can verify the
/// claim without the excerpt being duplicated at length.
/// </summary>
public sealed record MetadataReviewEvidenceSection(
    string Kind,
    string? Reference,
    string Text);

/// <summary>
/// Usage of a term inside the active profile. Only <see cref="Selectable"/>
/// terms may be suggested; structural ancestors are carried so hierarchy
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
/// The active Genre/Form policy, captured as a value so classification and its
/// validation stay independent of persistence. Built from the Knowledge Store
/// in production and from a fixture in evaluation.
/// </summary>
public sealed record GenreFormPolicySnapshot(
    string PolicyVersion,
    IReadOnlyList<GenreFormPolicyTerm> Terms,
    int MaximumSuggestions = GenreFormPolicySnapshot.DefaultMaximumSuggestions)
{
    /// <summary>
    /// Cardinality bound for one Work. The specification requires a bound
    /// without fixing a number; four leaves room beyond the two-term reference
    /// case while refusing a model that returns most of the vocabulary.
    /// </summary>
    public const int DefaultMaximumSuggestions = 4;

    public IEnumerable<GenreFormPolicyTerm> SelectableTerms =>
        Terms.Where(x => x.Usage == GenreFormPolicyUsage.Selectable);

    public GenreFormPolicyTerm? Find(string authorityUri)
    {
        return Terms.FirstOrDefault(
            x => string.Equals(x.AuthorityUri, authorityUri, StringComparison.Ordinal));
    }
}

/// <summary>
/// Supplies the active policy. Implemented over the Knowledge Store; kept as a
/// port so the classification contract never depends on persistence.
/// </summary>
public interface IGenreFormPolicyProvider
{
    Task<GenreFormPolicySnapshot> GetActivePolicyAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Identity of one classification run, retained so a suggestion can always be
/// attributed to the policy, prompt and model that produced it.
/// </summary>
public sealed record MetadataReviewAnalysisIdentity(
    string PolicyVersion,
    string PromptVersion,
    string ModelProvider,
    string ModelName,
    DateTimeOffset CreatedAt);

/// <summary>
/// Untrusted model output, before validation. Identifiers are carried as plain
/// strings precisely because the model may invent them.
/// </summary>
public sealed record RawGenreFormClassification(
    IReadOnlyList<RawGenreFormSuggestion> Suggested,
    IReadOnlyList<RawGenreFormRejection> ConsideredButRejected,
    bool InsufficientEvidence);

public sealed record RawGenreFormSuggestion(
    string? AuthorityId,
    string? Justification,
    IReadOnlyList<string> Evidence);

public sealed record RawGenreFormRejection(
    string? AuthorityId,
    string? Reason);

public sealed record GenreFormSuggestion(
    string AuthorityUri,
    string AuthorityIdentifier,
    string PreferredLabel,
    string Justification,
    IReadOnlyList<string> Evidence);

public sealed record GenreFormRejection(
    string AuthorityUri,
    string PreferredLabel,
    string Reason);

/// <summary>
/// A validated classification. Reaching this type means every identifier was
/// resolved against the active profile; nothing here was coerced.
/// </summary>
public sealed record GenreFormClassificationResult(
    MetadataReviewAnalysisIdentity Identity,
    IReadOnlyList<GenreFormSuggestion> Suggested,
    IReadOnlyList<GenreFormRejection> ConsideredButRejected,
    bool InsufficientEvidence);

public enum GenreFormValidationFailure
{
    MissingAuthorityId = 0,
    UnknownAuthorityTerm = 1,
    TermNotSelectable = 2,
    DuplicateSuggestion = 3,
    SuggestedAndRejected = 4,
    TooManySuggestions = 5,
    MissingJustification = 6,
    MissingRejectionReason = 7,
    RedundantHierarchy = 8,
    ContradictoryInsufficientEvidence = 9
}

public sealed record GenreFormValidationError(
    GenreFormValidationFailure Failure,
    string Detail);

/// <summary>
/// Validation outcome. <see cref="Result"/> is present only when the model
/// output was entirely valid: invalid output fails closed rather than being
/// partially salvaged.
/// </summary>
public sealed record GenreFormClassificationValidation(
    bool IsValid,
    GenreFormClassificationResult? Result,
    IReadOnlyList<GenreFormValidationError> Errors);

public interface IGenreFormClassificationValidator
{
    GenreFormClassificationValidation Validate(
        RawGenreFormClassification raw,
        GenreFormPolicySnapshot policy,
        MetadataReviewAnalysisIdentity identity);
}
