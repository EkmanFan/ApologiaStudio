using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.Application.Knowledge.MetadataReview;

/// <summary>
/// What a reviewer did with a machine proposal. Derived by comparing the
/// suggested set with the set the reviewer actually confirmed; it never makes
/// the suggestion store authoritative.
/// </summary>
public enum MetadataReviewOutcome
{
    Accepted = 0,
    Modified = 1,
    Rejected = 2
}

public enum MetadataReviewAnalysisStatus
{
    Valid = 0,
    Failed = 1
}

public enum MetadataReviewSuggestionDisposition
{
    Suggested = 0,
    ConsideredButRejected = 1
}

public sealed record MetadataReviewSuggestionRecord(
    string AuthorityUri,
    string AuthorityIdentifier,
    string PreferredLabel,
    MetadataReviewSuggestionDisposition Disposition,
    string Justification,
    IReadOnlyList<string> Evidence);

/// <summary>
/// One analysis run, kept as advisory history. Never authoritative metadata:
/// the reviewer's confirmed selection lives on the editorial draft, and Work
/// metadata only exists after publication.
/// </summary>
public sealed record MetadataReviewAnalysis(
    Guid Id,
    Guid DraftId,
    string Field,
    MetadataReviewAnalysisStatus Status,
    string? PolicyVersion,
    string? PromptVersion,
    string? ModelProvider,
    string? ModelName,
    bool InsufficientEvidence,
    string? FailureReason,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset CompletedAtUtc,
    double? DurationMilliseconds,
    Guid ActorUserId,
    Guid? SupersededByAnalysisId,
    MetadataReviewOutcome? ReviewerOutcome,
    Guid? ReviewerUserId,
    DateTimeOffset? ReviewedAtUtc,
    IReadOnlyList<MetadataReviewSuggestionRecord> Suggestions)
{
    public const string GenreFormField = "genre_form";

    public IEnumerable<MetadataReviewSuggestionRecord> SuggestedTerms =>
        Suggestions.Where(
            x => x.Disposition == MetadataReviewSuggestionDisposition.Suggested);
}

public sealed record RecordMetadataReviewAnalysisCommand(
    Guid DraftId,
    Guid ActorUserId,
    GenreFormClassificationResult Result,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset CompletedAtUtc,
    double? DurationMilliseconds);

public sealed record RecordFailedMetadataReviewAnalysisCommand(
    Guid DraftId,
    Guid ActorUserId,
    string FailureReason,
    string? PolicyVersion,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset CompletedAtUtc,
    double? DurationMilliseconds);

public interface IMetadataReviewAnalysisStore
{
    /// <summary>
    /// Appends a validated analysis and supersedes the previous current run
    /// for the same draft and field. Earlier analyses are never rewritten.
    /// </summary>
    Task<MetadataReviewAnalysis> RecordAsync(
        RecordMetadataReviewAnalysisCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Appends a diagnostic record for a run that produced nothing usable.
    /// A failed analysis carries no suggestion: invalid model output never
    /// becomes persisted advice.
    /// </summary>
    Task<MetadataReviewAnalysis> RecordFailureAsync(
        RecordFailedMetadataReviewAnalysisCommand command,
        CancellationToken cancellationToken);

    Task<MetadataReviewAnalysis?> GetCurrentAsync(
        Guid draftId,
        string field,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MetadataReviewAnalysis>> ListAsync(
        Guid draftId,
        string field,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records what the reviewer did, after their editorial save succeeded.
    /// </summary>
    Task RecordReviewerOutcomeAsync(
        Guid analysisId,
        MetadataReviewOutcome outcome,
        Guid reviewerUserId,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// Compares a machine proposal with the reviewer's confirmed selection.
/// </summary>
public static class MetadataReviewOutcomeCalculator
{
    public static MetadataReviewOutcome Determine(
        IReadOnlyList<string> suggestedAuthorityUris,
        IReadOnlyList<string> confirmedAuthorityUris)
    {
        ArgumentNullException.ThrowIfNull(suggestedAuthorityUris);
        ArgumentNullException.ThrowIfNull(confirmedAuthorityUris);

        var suggested = suggestedAuthorityUris.ToHashSet(StringComparer.Ordinal);
        var confirmed = confirmedAuthorityUris.ToHashSet(StringComparer.Ordinal);

        if (suggested.SetEquals(confirmed))
        {
            // Includes the case where nothing was proposed and nothing chosen:
            // the reviewer agreed that no term applies.
            return MetadataReviewOutcome.Accepted;
        }

        return suggested.Count > 0 && !suggested.Overlaps(confirmed)
            ? MetadataReviewOutcome.Rejected
            : MetadataReviewOutcome.Modified;
    }
}
