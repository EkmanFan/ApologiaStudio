namespace ApologiaStudio.Infrastructure.Persistence.Knowledge;

/// <summary>
/// One machine analysis run over an editorial draft. Advisory history, never
/// authoritative metadata: superseded runs stay readable so a decision can be
/// reconstructed later.
/// </summary>
internal sealed class MetadataReviewAnalysisEntity
{
    public Guid Id { get; set; }

    public Guid DraftId { get; set; }

    public string Field { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? PolicyVersion { get; set; }

    public string? PromptVersion { get; set; }

    public string? ModelProvider { get; set; }

    public string? ModelName { get; set; }

    public bool InsufficientEvidence { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }

    public double? DurationMilliseconds { get; set; }

    public Guid ActorUserId { get; set; }

    public Guid? SupersededByAnalysisId { get; set; }

    public string? ReviewerOutcome { get; set; }

    public Guid? ReviewerUserId { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }
}

internal sealed class MetadataReviewSuggestionEntity
{
    public long Id { get; set; }

    public Guid AnalysisId { get; set; }

    public Guid TermId { get; set; }

    public string Disposition { get; set; } = string.Empty;

    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// A stable reference backing one suggestion. References are kept rather than
/// excerpts, so history never duplicates source text.
/// </summary>
internal sealed class MetadataReviewSuggestionEvidenceEntity
{
    public long Id { get; set; }

    public long SuggestionId { get; set; }

    public int Ordinal { get; set; }

    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Set instead of <see cref="SuggestionId"/> when the parent key is still
    /// database-generated; EF resolves the foreign key on save.
    /// </summary>
    public MetadataReviewSuggestionEntity? Suggestion { get; set; }
}
