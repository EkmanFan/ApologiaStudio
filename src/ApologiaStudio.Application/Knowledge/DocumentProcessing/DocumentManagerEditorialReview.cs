using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Abstractions.Identity;

namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public enum DocumentManagerEditorialReviewAction
{
    Save = 0,
    Approve = 1,
    Reject = 2,
    Reopen = 3
}

public sealed record DocumentManagerEditorialDraftSummary(
    Guid Id,
    string Title,
    string OriginalFileName,
    DocumentManagerEditorialDraftStatus Status,
    int PartCount,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record DocumentManagerEditorialDraftReviewCommand(
    Guid DraftId,
    int ExpectedVersion,
    DocumentManagerEditorialReviewAction Action,
    string Title,
    string? PrimaryContributorName,
    string? PrimaryContributorRole,
    string? LanguageCode,
    string? EditionStatement,
    int? PublicationYear,
    string? PublicationPlace,
    string? Description,
    IReadOnlyList<string> GenreFormAuthorityUris,
    string? RejectionReason);

public sealed record DocumentManagerEditorialDraftMutation(
    Guid DraftId,
    int ExpectedVersion,
    DocumentManagerEditorialReviewAction Action,
    string Title,
    string TitleOrigin,
    string? PrimaryContributorName,
    string? PrimaryContributorRole,
    string? LanguageCode,
    string? EditionStatement,
    int? PublicationYear,
    string? PublicationPlace,
    string? Description,
    // Authority URIs of the reviewer's selection; part of the same mutation
    // and therefore of the same optimistic-concurrency check.
    IReadOnlyList<string> GenreFormAuthorityUris,
    DocumentManagerEditorialDraftStatus TargetStatus,
    Guid ActorUserId,
    DateTimeOffset OccurredAtUtc,
    string? RejectionReason);

public interface IDocumentManagerEditorialReviewStore
{
    Task<IReadOnlyList<DocumentManagerEditorialDraftSummary>> ListAsync(
        CancellationToken cancellationToken);

    Task<DocumentManagerEditorialDraft?> GetAsync(
        Guid draftId,
        CancellationToken cancellationToken);

    Task<DocumentManagerEditorialDraft> ApplyAsync(
        DocumentManagerEditorialDraftMutation mutation,
        CancellationToken cancellationToken);
}

public sealed class ListDocumentManagerEditorialDraftsHandler(
    IDocumentManagerEditorialReviewStore store)
{
    public Task<IReadOnlyList<DocumentManagerEditorialDraftSummary>> HandleAsync(
        CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);
}

public sealed class GetDocumentManagerEditorialDraftHandler(
    IDocumentManagerEditorialReviewStore store)
{
    public Task<DocumentManagerEditorialDraft?> HandleAsync(
        Guid draftId,
        CancellationToken cancellationToken) =>
        store.GetAsync(draftId, cancellationToken);
}

public interface IDocumentManagerAdministrationAuthorizer
{
    bool IsAuthorized { get; }
}

public sealed record ReopenDocumentManagerEditorialDraftCommand(
    Guid DraftId,
    int ExpectedVersion);

public sealed class ReopenDocumentManagerEditorialDraftHandler(
    IDocumentManagerEditorialReviewStore store,
    IDocumentManagerAdministrationAuthorizer authorizer,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<DocumentManagerEditorialDraft> HandleAsync(
        ReopenDocumentManagerEditorialDraftCommand command,
        CancellationToken cancellationToken)
    {
        EnsureAuthorized(authorizer);
        ArgumentNullException.ThrowIfNull(command);

        if (command.DraftId == Guid.Empty || command.ExpectedVersion < 0)
        {
            throw new ArgumentException(
                "Draft identifier and expected version are invalid.",
                nameof(command));
        }

        var draft = await store.GetAsync(
            command.DraftId,
            cancellationToken) ?? throw new KeyNotFoundException(
            $"Editorial draft '{command.DraftId:D}' was not found.");

        if (draft.Version != command.ExpectedVersion)
        {
            throw new DocumentManagerEditorialDraftConcurrencyException(
                command.DraftId);
        }

        if (draft.Status != DocumentManagerEditorialDraftStatus.Rejected)
        {
            throw new DocumentManagerEditorialReviewValidationException(
                "Only a rejected editorial draft can be reopened.");
        }

        return await store.ApplyAsync(
            new DocumentManagerEditorialDraftMutation(
                draft.Id,
                draft.Version,
                DocumentManagerEditorialReviewAction.Reopen,
                draft.Title,
                draft.TitleOrigin,
                draft.PrimaryContributorName,
                draft.PrimaryContributorRole,
                draft.LanguageCode,
                draft.EditionStatement,
                draft.PublicationYear,
                draft.PublicationPlace,
                draft.Description,
                draft.GenreForms.Select(x => x.AuthorityUri).ToList(),
                DocumentManagerEditorialDraftStatus.PendingReview,
                currentUser.UserId.Value,
                timeProvider.GetUtcNow(),
                null),
            cancellationToken);
    }

    internal static void EnsureAuthorized(
        IDocumentManagerAdministrationAuthorizer authorizer)
    {
        if (!authorizer.IsAuthorized)
        {
            throw new DocumentManagerAdministrationForbiddenException();
        }
    }
}

public sealed class DocumentManagerAdministrationForbiddenException()
    : InvalidOperationException(
        "Document Manager administrative actions are not authorized.");

public sealed class ReviewDocumentManagerEditorialDraftHandler(
    IDocumentManagerEditorialReviewStore store,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IGenreFormPolicyProvider genreFormPolicyProvider)
{
    private static readonly HashSet<string> AllowedContributorRoles =
        new(StringComparer.Ordinal)
        {
            "author",
            "compiler",
            "translator",
            "textual_editor"
        };

    public async Task<DocumentManagerEditorialDraft> HandleAsync(
        DocumentManagerEditorialDraftReviewCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DraftId == Guid.Empty || command.ExpectedVersion < 0)
        {
            throw new ArgumentException(
                "Draft identifier and expected version are invalid.",
                nameof(command));
        }

        var title = RequireText(command.Title, "Title", 1000);
        var contributorName =
            NormalizeOptional(
                command.PrimaryContributorName,
                "Primary contributor name",
                500);
        var contributorRole =
            NormalizeOptional(
                command.PrimaryContributorRole,
                "Primary contributor role",
                64);
        var languageCode =
            NormalizeOptional(command.LanguageCode, "Language code", 35);
        var editionStatement =
            NormalizeOptional(
                command.EditionStatement,
                "Edition statement",
                500);
        var publicationPlace =
            NormalizeOptional(
                command.PublicationPlace,
                "Publication place",
                500);
        var description =
            NormalizeOptional(command.Description, "Description", 20_000);
        var rejectionReason =
            NormalizeOptional(
                command.RejectionReason,
                "Rejection reason",
                4000);

        if ((contributorName is null) != (contributorRole is null))
        {
            throw new DocumentManagerEditorialReviewValidationException(
                "The primary contributor name and role must be provided together.");
        }

        if (contributorRole is not null &&
            !AllowedContributorRoles.Contains(contributorRole))
        {
            throw new DocumentManagerEditorialReviewValidationException(
                $"Unsupported primary contributor role '{contributorRole}'.");
        }

        if (command.PublicationYear is int year && year is < 1 or > 9999)
        {
            throw new DocumentManagerEditorialReviewValidationException(
                "Publication year must be between 1 and 9999.");
        }

        if (command.Action == DocumentManagerEditorialReviewAction.Approve &&
            (languageCode is null || contributorName is null))
        {
            throw new DocumentManagerEditorialReviewValidationException(
                "Approval requires a title, language, and primary contributor.");
        }

        if (command.Action == DocumentManagerEditorialReviewAction.Reject &&
            rejectionReason is null)
        {
            throw new DocumentManagerEditorialReviewValidationException(
                "Rejection requires a reason.");
        }

        var targetStatus = command.Action switch
        {
            DocumentManagerEditorialReviewAction.Save =>
                DocumentManagerEditorialDraftStatus.InReview,
            DocumentManagerEditorialReviewAction.Approve =>
                DocumentManagerEditorialDraftStatus.Approved,
            DocumentManagerEditorialReviewAction.Reject =>
                DocumentManagerEditorialDraftStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(
                nameof(command.Action),
                command.Action,
                null)
        };

        var genreFormAuthorityUris = await ResolveGenreFormsAsync(
            command.GenreFormAuthorityUris,
            cancellationToken);

        return await store.ApplyAsync(
            new DocumentManagerEditorialDraftMutation(
                command.DraftId,
                command.ExpectedVersion,
                command.Action,
                title,
                "editorial",
                contributorName,
                contributorRole,
                languageCode,
                editionStatement,
                command.PublicationYear,
                publicationPlace,
                description,
                genreFormAuthorityUris,
                targetStatus,
                currentUser.UserId.Value,
                timeProvider.GetUtcNow(),
                command.Action == DocumentManagerEditorialReviewAction.Reject
                    ? rejectionReason
                    : null),
            cancellationToken);
    }

    /// <summary>
    /// Validates the reviewer's selection through the shared Genre/Form rules,
    /// so a hand-made choice is judged exactly as an assisted one. The
    /// vocabulary is never restated here.
    /// </summary>
    private async Task<IReadOnlyList<string>> ResolveGenreFormsAsync(
        IReadOnlyList<string>? requested,
        CancellationToken cancellationToken)
    {
        if (requested is null || requested.Count == 0)
        {
            return [];
        }

        var policy = await genreFormPolicyProvider.GetActivePolicyAsync(
            cancellationToken);

        var errors = GenreFormSelectionRules.Validate(requested, policy);

        if (errors.Count > 0)
        {
            throw new DocumentManagerEditorialReviewValidationException(
                string.Join(" ", errors.Select(x => x.Detail)));
        }

        return requested
            .Select(x => GenreFormSelectionRules.Resolve(x, policy)!.AuthorityUri)
            .ToList();
    }

    private static string RequireText(
        string value,
        string label,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DocumentManagerEditorialReviewValidationException(
                $"{label} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DocumentManagerEditorialReviewValidationException(
                $"{label} cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        string label,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DocumentManagerEditorialReviewValidationException(
                $"{label} cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }
}

public sealed class DocumentManagerEditorialReviewValidationException(
    string message)
    : Exception(message);

public sealed class DocumentManagerEditorialDraftConcurrencyException(
    Guid draftId)
    : Exception(
        $"Editorial draft '{draftId:D}' was changed by another review session.");
