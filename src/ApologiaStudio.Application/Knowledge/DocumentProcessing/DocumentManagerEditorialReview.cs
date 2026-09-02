using ApologiaStudio.Application.Abstractions.Identity;

namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public enum DocumentManagerEditorialReviewAction
{
    Save = 0,
    Approve = 1,
    Reject = 2
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

public sealed class ReviewDocumentManagerEditorialDraftHandler(
    IDocumentManagerEditorialReviewStore store,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly HashSet<string> AllowedContributorRoles =
        new(StringComparer.Ordinal)
        {
            "author",
            "compiler",
            "translator",
            "textual_editor"
        };

    public Task<DocumentManagerEditorialDraft> HandleAsync(
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

        return store.ApplyAsync(
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
                targetStatus,
                currentUser.UserId.Value,
                timeProvider.GetUtcNow(),
                command.Action == DocumentManagerEditorialReviewAction.Reject
                    ? rejectionReason
                    : null),
            cancellationToken);
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
