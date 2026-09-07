namespace ApologiaStudio.Infrastructure.Persistence.Knowledge;

internal sealed class GenreFormAuthoritySnapshotEntity
{
    public Guid Id { get; set; }

    public string Authority { get; set; } = string.Empty;

    public string SourceUri { get; set; } = string.Empty;

    public string ContentSha256 { get; set; } = string.Empty;

    public DateTimeOffset RetrievedAt { get; set; }

    public string? ImporterVersion { get; set; }

    public int TermCount { get; set; }
}

internal sealed class GenreFormAuthorityTermEntity
{
    public Guid Id { get; set; }

    public string Authority { get; set; } = string.Empty;

    public string AuthorityIdentifier { get; set; } = string.Empty;

    public string AuthorityUri { get; set; } = string.Empty;

    public string PreferredLabel { get; set; } = string.Empty;

    public string? LanguageCode { get; set; }

    public string AuthorityStatus { get; set; } = "active";

    public Guid SnapshotId { get; set; }
}

internal sealed class GenreFormAuthorityVariantEntity
{
    public long Id { get; set; }

    public Guid TermId { get; set; }

    public string Label { get; set; } = string.Empty;

    public string? LanguageCode { get; set; }
}

internal sealed class GenreFormAuthorityNoteEntity
{
    public long Id { get; set; }

    public Guid TermId { get; set; }

    public string NoteType { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

internal sealed class GenreFormBroaderRelationEntity
{
    public long Id { get; set; }

    public Guid NarrowerTermId { get; set; }

    public Guid BroaderTermId { get; set; }
}

/// <summary>
/// Associative, non-hierarchical relation. Stored canonically with the lower
/// identifier first so a symmetric pair is never duplicated in reverse order.
/// </summary>
internal sealed class GenreFormRelatedRelationEntity
{
    public long Id { get; set; }

    public Guid TermIdA { get; set; }

    public Guid TermIdB { get; set; }
}

/// <summary>
/// The authoritative Genre/Form assignment of a Work.
/// </summary>
/// <remarks>
/// Points at the Apologia product taxonomy, never at an external authority
/// catalogue: what a Work carries is a product concept, and the alignment of
/// that concept to LCGFT or BnF is a separate, replaceable fact.
/// </remarks>
internal sealed class KnowledgeWorkGenreFormEntity
{
    public long Id { get; set; }

    public Guid WorkId { get; set; }

    public Guid ProductTermId { get; set; }
}

/// <summary>
/// The reviewer's pre-publication Genre/Form selection on an editorial draft.
///
/// Owned by the editorial-review workflow, not by the metadata assistant: it
/// holds human-reviewed choices, never raw machine suggestions. AS-DM-06 will
/// later project it into <c>knowledge_work_genre_forms</c>.
/// </summary>
internal sealed class DocumentManagerEditorialDraftGenreFormEntity
{
    public long Id { get; set; }

    public Guid DraftId { get; set; }

    public Guid ProductTermId { get; set; }
}

/// <summary>
/// One canonical Apologia Genre/Form product term.
/// </summary>
/// <remarks>
/// Product-owned identity, independent of any external bibliographic authority.
/// Alignment to LCGFT or BnF is recorded separately and never defines the
/// concept.
/// </remarks>
internal sealed class ApologiaGenreFormTermEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string PreferredLabel { get; set; } = string.Empty;

    public string Definition { get; set; } = string.Empty;

    public string PredictionMode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string TaxonomyVersion { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// One recorded alignment between an Apologia product term and an external
/// authority concept.
/// </summary>
/// <remarks>
/// <see cref="ExternalConceptId"/> is deliberately not a foreign key. Aligning
/// to an authority must not require importing that whole authority locally,
/// otherwise a first BnF alignment would be blocked by a bulk import nobody
/// asked for. Existence of the external concept is enforced by whichever seeder
/// owns that authority, where the check can be made against the catalogue that
/// is actually present.
/// </remarks>
internal sealed class GenreFormAuthorityMappingEntity
{
    public Guid Id { get; set; }

    public Guid ProductTermId { get; set; }

    public string Authority { get; set; } = string.Empty;

    public string ExternalConceptId { get; set; } = string.Empty;

    public string? ExternalConceptUri { get; set; }

    public string MappingKind { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
