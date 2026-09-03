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

internal sealed class GenreFormProfileEntryEntity
{
    public Guid TermId { get; set; }

    public string UsageStatus { get; set; } = "excluded";

    public int? DisplayOrder { get; set; }

    public string ProfileVersion { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class KnowledgeWorkGenreFormEntity
{
    public long Id { get; set; }

    public Guid WorkId { get; set; }

    public Guid TermId { get; set; }
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

    public Guid TermId { get; set; }
}
