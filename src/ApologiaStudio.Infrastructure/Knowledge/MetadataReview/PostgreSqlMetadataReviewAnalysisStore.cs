using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.MetadataReview;

/// <summary>
/// Append-only history of machine analyses over an editorial draft.
///
/// A new run supersedes the previous current one by pointing it forward; no
/// analysis or suggestion is ever rewritten or deleted. This store holds
/// advisory evidence only — the reviewer's confirmed selection lives on the
/// draft, and Work metadata only exists after publication.
/// </summary>
public sealed class PostgreSqlMetadataReviewAnalysisStore(
    KnowledgeDbContext context)
    : IMetadataReviewAnalysisStore
{
    public async Task<MetadataReviewAnalysis> RecordAsync(
        RecordMetadataReviewAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var identity = command.Result.Identity;
        var analysisId = Guid.CreateVersion7();

        var entity = new MetadataReviewAnalysisEntity
        {
            Id = analysisId,
            DraftId = command.DraftId,
            Field = MetadataReviewAnalysis.GenreFormField,
            Status = "valid",
            PolicyVersion = identity.PolicyVersion,
            PromptVersion = identity.PromptVersion,
            ModelProvider = identity.ModelProvider,
            ModelName = identity.ModelName,
            InsufficientEvidence = command.Result.InsufficientEvidence,
            RequestedAtUtc = command.RequestedAtUtc,
            CompletedAtUtc = command.CompletedAtUtc,
            DurationMilliseconds = command.DurationMilliseconds,
            ActorUserId = command.ActorUserId
        };

        context.MetadataReviewAnalyses.Add(entity);

        await AddSuggestionsAsync(analysisId, command.Result, cancellationToken);
        await SupersedePreviousAsync(command.DraftId, analysisId, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return await RequireAsync(analysisId, cancellationToken);
    }

    public async Task<MetadataReviewAnalysis> RecordFailureAsync(
        RecordFailedMetadataReviewAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FailureReason);

        var analysisId = Guid.CreateVersion7();

        context.MetadataReviewAnalyses.Add(
            new MetadataReviewAnalysisEntity
            {
                Id = analysisId,
                DraftId = command.DraftId,
                Field = MetadataReviewAnalysis.GenreFormField,
                Status = "failed",
                PolicyVersion = command.PolicyVersion,
                InsufficientEvidence = false,
                // A failed run carries no suggestion: invalid model output is
                // recorded as a diagnostic, never as advice.
                FailureReason = command.FailureReason,
                RequestedAtUtc = command.RequestedAtUtc,
                CompletedAtUtc = command.CompletedAtUtc,
                DurationMilliseconds = command.DurationMilliseconds,
                ActorUserId = command.ActorUserId
            });

        await SupersedePreviousAsync(command.DraftId, analysisId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await RequireAsync(analysisId, cancellationToken);
    }

    public async Task<MetadataReviewAnalysis?> GetCurrentAsync(
        Guid draftId,
        string field,
        CancellationToken cancellationToken)
    {
        var current = await context.MetadataReviewAnalyses
            .AsNoTracking()
            .Where(x => x.DraftId == draftId &&
                        x.Field == field &&
                        x.SupersededByAnalysisId == null)
            .OrderByDescending(x => x.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return current is null
            ? null
            : await ToContractAsync(current, cancellationToken);
    }

    public async Task<IReadOnlyList<MetadataReviewAnalysis>> ListAsync(
        Guid draftId,
        string field,
        CancellationToken cancellationToken)
    {
        var entities = await context.MetadataReviewAnalyses
            .AsNoTracking()
            .Where(x => x.DraftId == draftId && x.Field == field)
            .OrderByDescending(x => x.CompletedAtUtc)
            .ToListAsync(cancellationToken);

        var analyses = new List<MetadataReviewAnalysis>();

        foreach (var entity in entities)
        {
            analyses.Add(await ToContractAsync(entity, cancellationToken));
        }

        return analyses;
    }

    public async Task RecordReviewerOutcomeAsync(
        Guid analysisId,
        MetadataReviewOutcome outcome,
        Guid reviewerUserId,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var entity = await context.MetadataReviewAnalyses
            .FirstOrDefaultAsync(x => x.Id == analysisId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.ReviewerOutcome = outcome switch
        {
            MetadataReviewOutcome.Accepted => "accepted",
            MetadataReviewOutcome.Modified => "modified",
            _ => "rejected"
        };
        entity.ReviewerUserId = reviewerUserId;
        entity.ReviewedAtUtc = reviewedAtUtc;

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task AddSuggestionsAsync(
        Guid analysisId,
        Application.Knowledge.MetadataReview.GenreFormClassificationResult result,
        CancellationToken cancellationToken)
    {
        var uris = result.Suggested
            .Select(x => x.AuthorityUri)
            .Concat(result.ConsideredButRejected.Select(x => x.AuthorityUri))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (uris.Count == 0)
        {
            return;
        }

        var termIds = await context.GenreFormTerms
            .AsNoTracking()
            .Where(x => uris.Contains(x.AuthorityUri))
            .Select(x => new { x.Id, x.AuthorityUri })
            .ToDictionaryAsync(x => x.AuthorityUri, x => x.Id, cancellationToken);

        foreach (var suggestion in result.Suggested)
        {
            var entity = new MetadataReviewSuggestionEntity
            {
                AnalysisId = analysisId,
                TermId = termIds[suggestion.AuthorityUri],
                Disposition = "suggested",
                Justification = suggestion.Justification
            };

            context.MetadataReviewSuggestions.Add(entity);

            var ordinal = 0;
            foreach (var reference in suggestion.Evidence)
            {
                context.MetadataReviewSuggestionEvidence.Add(
                    new MetadataReviewSuggestionEvidenceEntity
                    {
                        Suggestion = entity,
                        Ordinal = ordinal++,
                        Reference = reference
                    });
            }
        }

        foreach (var rejection in result.ConsideredButRejected)
        {
            context.MetadataReviewSuggestions.Add(
                new MetadataReviewSuggestionEntity
                {
                    AnalysisId = analysisId,
                    TermId = termIds[rejection.AuthorityUri],
                    Disposition = "considered_but_rejected",
                    Justification = rejection.Reason
                });
        }
    }

    /// <summary>
    /// Points the previous current run at the new one. History is preserved:
    /// nothing is deleted and no earlier suggestion changes.
    /// </summary>
    private async Task SupersedePreviousAsync(
        Guid draftId,
        Guid newAnalysisId,
        CancellationToken cancellationToken)
    {
        var previous = await context.MetadataReviewAnalyses
            .Where(x => x.DraftId == draftId &&
                        x.Field == MetadataReviewAnalysis.GenreFormField &&
                        x.SupersededByAnalysisId == null &&
                        x.Id != newAnalysisId)
            .ToListAsync(cancellationToken);

        foreach (var entity in previous)
        {
            entity.SupersededByAnalysisId = newAnalysisId;
        }
    }

    private async Task<MetadataReviewAnalysis> RequireAsync(
        Guid analysisId,
        CancellationToken cancellationToken)
    {
        var entity = await context.MetadataReviewAnalyses
            .AsNoTracking()
            .FirstAsync(x => x.Id == analysisId, cancellationToken);

        return await ToContractAsync(entity, cancellationToken);
    }

    private async Task<MetadataReviewAnalysis> ToContractAsync(
        MetadataReviewAnalysisEntity entity,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from suggestion in context.MetadataReviewSuggestions.AsNoTracking()
            join term in context.GenreFormTerms.AsNoTracking()
                on suggestion.TermId equals term.Id
            where suggestion.AnalysisId == entity.Id
            orderby suggestion.Id
            select new
            {
                suggestion.Id,
                term.AuthorityUri,
                term.AuthorityIdentifier,
                term.PreferredLabel,
                suggestion.Disposition,
                suggestion.Justification
            })
            .ToListAsync(cancellationToken);

        var suggestionIds = rows.Select(x => x.Id).ToList();

        var evidence = await context.MetadataReviewSuggestionEvidence
            .AsNoTracking()
            .Where(x => suggestionIds.Contains(x.SuggestionId))
            .OrderBy(x => x.Ordinal)
            .Select(x => new { x.SuggestionId, x.Reference })
            .ToListAsync(cancellationToken);

        var bySuggestion = evidence
            .GroupBy(x => x.SuggestionId)
            .ToDictionary(x => x.Key, x => x.Select(e => e.Reference).ToList());

        var suggestions = rows
            .Select(row => new MetadataReviewSuggestionRecord(
                row.AuthorityUri,
                row.AuthorityIdentifier,
                row.PreferredLabel,
                row.Disposition == "suggested"
                    ? MetadataReviewSuggestionDisposition.Suggested
                    : MetadataReviewSuggestionDisposition.ConsideredButRejected,
                row.Justification,
                bySuggestion.TryGetValue(row.Id, out var references)
                    ? references
                    : []))
            .ToList();

        return new MetadataReviewAnalysis(
            entity.Id,
            entity.DraftId,
            entity.Field,
            entity.Status == "failed"
                ? MetadataReviewAnalysisStatus.Failed
                : MetadataReviewAnalysisStatus.Valid,
            entity.PolicyVersion,
            entity.PromptVersion,
            entity.ModelProvider,
            entity.ModelName,
            entity.InsufficientEvidence,
            entity.FailureReason,
            entity.RequestedAtUtc,
            entity.CompletedAtUtc,
            entity.DurationMilliseconds,
            entity.ActorUserId,
            entity.SupersededByAnalysisId,
            entity.ReviewerOutcome switch
            {
                "accepted" => MetadataReviewOutcome.Accepted,
                "modified" => MetadataReviewOutcome.Modified,
                "rejected" => MetadataReviewOutcome.Rejected,
                _ => null
            },
            entity.ReviewerUserId,
            entity.ReviewedAtUtc,
            suggestions);
    }
}
