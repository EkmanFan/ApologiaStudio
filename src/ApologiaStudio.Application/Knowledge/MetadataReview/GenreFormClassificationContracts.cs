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
    string? TermCode,
    string? Justification,
    IReadOnlyList<string> Evidence);

public sealed record RawGenreFormRejection(
    string? TermCode,
    string? Reason);

/// <summary>
/// One validated proposal, carrying the Apologia product identity. No LCGFT
/// URI or authority identifier travels with a suggestion: alignment is a
/// separate fact and never the identity of what is proposed.
/// </summary>
public sealed record GenreFormSuggestion(
    string Code,
    string PreferredLabel,
    string Justification,
    IReadOnlyList<string> Evidence);

public sealed record GenreFormRejection(
    string Code,
    string PreferredLabel,
    string Reason);

/// <summary>
/// A validated classification. Reaching this type means every identifier was
/// resolved against the active product taxonomy; nothing here was coerced.
/// </summary>
public sealed record GenreFormClassificationResult(
    MetadataReviewAnalysisIdentity Identity,
    IReadOnlyList<GenreFormSuggestion> Suggested,
    IReadOnlyList<GenreFormRejection> ConsideredButRejected,
    bool InsufficientEvidence);

public enum GenreFormValidationFailure
{
    MissingTermCode = 0,
    UnknownTerm = 1,
    DuplicateSuggestion = 2,
    SuggestedAndRejected = 3,
    TooManySuggestions = 4,
    MissingJustification = 5,
    MissingRejectionReason = 6,
    ContradictoryInsufficientEvidence = 7
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
