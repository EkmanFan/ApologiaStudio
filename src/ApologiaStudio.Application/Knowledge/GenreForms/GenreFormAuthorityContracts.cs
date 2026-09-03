using ApologiaStudio.Application.Knowledge.Ingestion;

namespace ApologiaStudio.Application.Knowledge.GenreForms;

/// <summary>
/// Local interpretation of the authority's own evidence about a term.
/// The V1 bulk source does not carry a status flag, so <see cref="Deprecated"/>
/// is derived from change-set evidence rather than read from a field.
/// </summary>
public enum GenreFormAuthorityStatus
{
    Active = 0,
    Deprecated = 1
}

/// <summary>
/// Note kinds actually supplied by the authority. No scope note exists in the
/// V1 source and none is fabricated.
/// </summary>
public enum GenreFormNoteType
{
    General = 0,
    History = 1,
    Example = 2
}

/// <summary>
/// How Apologia may use an authority term. Independent from
/// <see cref="GenreFormAuthorityStatus"/>: an authority refresh never changes it.
/// </summary>
public enum GenreFormUsageStatus
{
    Excluded = 0,
    StructuralOnly = 1,
    Selectable = 2
}

public sealed record GenreFormAuthorityNote(
    GenreFormNoteType NoteType,
    string Text);

public sealed record GenreFormAuthorityVariant(
    string Label,
    string? LanguageCode);

/// <summary>
/// One authority term as acquired from an external vocabulary, expressed
/// without reference to any serialization format.
/// </summary>
public sealed record GenreFormAuthorityTerm(
    string AuthorityUri,
    string AuthorityIdentifier,
    string PreferredLabel,
    string? LanguageCode,
    GenreFormAuthorityStatus Status,
    IReadOnlyList<GenreFormAuthorityVariant> Variants,
    IReadOnlyList<GenreFormAuthorityNote> Notes,
    IReadOnlyList<string> BroaderAuthorityUris,
    IReadOnlyList<string> RelatedAuthorityUris)
{
    /// <summary>
    /// Stable local identity derived from the canonical authority URI, so that
    /// re-importing the same term never creates a second row.
    /// </summary>
    public Guid Id =>
        KnowledgeStableIds.ForAuthority(AuthorityUri);
}

/// <summary>
/// Identity of one acquired authority dataset. The Library of Congress bulk
/// source exposes no usable version identifier, so identity is the SHA-256 of
/// the imported content; retrieval time and source URI are separate metadata.
/// </summary>
public sealed record GenreFormAuthoritySnapshot(
    string Authority,
    string SourceUri,
    string ContentSha256,
    DateTimeOffset RetrievedAt,
    string? ImporterVersion);

public sealed record GenreFormAuthorityDataset(
    IReadOnlyList<GenreFormAuthorityTerm> Terms);

/// <summary>
/// A profile entry or Work assignment that references a term which the newly
/// imported snapshot no longer publishes as active. Reported for explicit
/// human review; never remapped automatically.
/// </summary>
public sealed record GenreFormProfileReviewItem(
    string AuthorityUri,
    string PreferredLabel,
    GenreFormAuthorityStatus Status,
    bool PresentInSnapshot,
    GenreFormUsageStatus UsageStatus,
    int WorkAssignmentCount);

public sealed record GenreFormAuthorityImportResult(
    Guid SnapshotId,
    string ContentSha256,
    bool SnapshotAlreadyImported,
    int TermCount,
    int DeprecatedTermCount,
    int VariantCount,
    int NoteCount,
    int BroaderRelationCount,
    int RelatedRelationCount,
    IReadOnlyList<GenreFormProfileReviewItem> ProfileReviewItems);

/// <summary>
/// Read projection of an authority term together with its Apologia usage.
/// </summary>
public sealed record GenreFormTermView(
    Guid Id,
    string AuthorityUri,
    string AuthorityIdentifier,
    string PreferredLabel,
    GenreFormAuthorityStatus Status,
    GenreFormUsageStatus UsageStatus,
    int? DisplayOrder);

/// <summary>
/// Reads an acquired authority payload into the serialization-agnostic model.
/// Implemented by an adapter that owns the concrete representation.
/// </summary>
public interface IGenreFormAuthorityDatasetReader
{
    string RepresentationId { get; }

    GenreFormAuthorityDataset Read(Stream content);
}

public interface IGenreFormAuthorityStore
{
    Task<GenreFormAuthorityImportResult> ImportAsync(
        GenreFormAuthoritySnapshot snapshot,
        GenreFormAuthorityDataset dataset,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GenreFormTermView>> GetSelectableTermsAsync(
        CancellationToken cancellationToken);

    Task<GenreFormTermView?> GetTermByAuthorityUriAsync(
        string authorityUri,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GenreFormTermView>> GetBroaderTermsAsync(
        string authorityUri,
        CancellationToken cancellationToken);

    /// <summary>
    /// Derived from the persisted broader relation; narrower is never stored
    /// as a second source of truth.
    /// </summary>
    Task<IReadOnlyList<GenreFormTermView>> GetNarrowerTermsAsync(
        string authorityUri,
        CancellationToken cancellationToken);
}

public sealed class GenreFormAuthorityException : Exception
{
    public GenreFormAuthorityException(string message)
        : base(message)
    {
    }

    public GenreFormAuthorityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
