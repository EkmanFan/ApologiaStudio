using ApologiaStudio.Application.Knowledge.GenreForms;

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

/// <summary>
/// Guards owned by the Metadata Review Assistant. They bound what the
/// assistant will accept from a model; they are not Genre/Form domain rules,
/// and the vocabulary itself imposes no cardinality.
/// </summary>
public sealed record MetadataReviewOptions(
    int MaximumSuggestions = MetadataReviewOptions.DefaultMaximumSuggestions)
{
    /// <summary>
    /// Leaves room beyond the two-term reference case while refusing a model
    /// that returns most of the vocabulary.
    /// </summary>
    public const int DefaultMaximumSuggestions = 4;

    public static MetadataReviewOptions Default { get; } = new();
}

public interface IGenreFormClassificationValidator
{
    GenreFormClassificationValidation Validate(
        RawGenreFormClassification raw,
        GenreFormPolicySnapshot policy,
        MetadataReviewAnalysisIdentity identity);
}
