using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

public sealed class DocumentManagerEditorialReviewTests
{
    private static readonly Guid DraftId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid UserIdValue =
        Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 2, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Save_normalizes_metadata_and_records_the_editor()
    {
        var store = new StubReviewStore();
        var handler = CreateHandler(store);

        await handler.HandleAsync(
            Command(
                DocumentManagerEditorialReviewAction.Save,
                title: "  A reviewed title  ",
                contributorName: "  Gary Habermas ",
                contributorRole: "author",
                languageCode: " en "),
            CancellationToken.None);

        Assert.NotNull(store.Received);
        Assert.Equal("A reviewed title", store.Received.Title);
        Assert.Equal("Gary Habermas", store.Received.PrimaryContributorName);
        Assert.Equal("en", store.Received.LanguageCode);
        Assert.Equal("editorial", store.Received.TitleOrigin);
        Assert.Equal(
            DocumentManagerEditorialDraftStatus.InReview,
            store.Received.TargetStatus);
        Assert.Equal(UserIdValue, store.Received.ActorUserId);
        Assert.Equal(Now, store.Received.OccurredAtUtc);
    }

    [Fact]
    public async Task Approval_requires_language_and_primary_contributor()
    {
        var handler = CreateHandler(new StubReviewStore());

        var exception = await Assert.ThrowsAsync<
            DocumentManagerEditorialReviewValidationException>(
            () => handler.HandleAsync(
                Command(DocumentManagerEditorialReviewAction.Approve),
                CancellationToken.None));

        Assert.Contains("Approval requires", exception.Message);
    }

    [Fact]
    public async Task Rejection_requires_a_reason()
    {
        var handler = CreateHandler(new StubReviewStore());

        var exception = await Assert.ThrowsAsync<
            DocumentManagerEditorialReviewValidationException>(
            () => handler.HandleAsync(
                Command(DocumentManagerEditorialReviewAction.Reject),
                CancellationToken.None));

        Assert.Contains("reason", exception.Message);
    }

    private static ReviewDocumentManagerEditorialDraftHandler CreateHandler(
        StubReviewStore store) =>
        new(
            store,
            new StubCurrentUser(),
            new FixedTimeProvider(Now));

    private static DocumentManagerEditorialDraftReviewCommand Command(
        DocumentManagerEditorialReviewAction action,
        string title = "A title",
        string? contributorName = null,
        string? contributorRole = null,
        string? languageCode = null,
        string? rejectionReason = null) =>
        new(
            DraftId,
            0,
            action,
            title,
            contributorName,
            contributorRole,
            languageCode,
            null,
            null,
            null,
            null,
            rejectionReason);

    private sealed class StubReviewStore : IDocumentManagerEditorialReviewStore
    {
        public DocumentManagerEditorialDraftMutation? Received { get; private set; }

        public Task<IReadOnlyList<DocumentManagerEditorialDraftSummary>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DocumentManagerEditorialDraftSummary>>([]);

        public Task<DocumentManagerEditorialDraft?> GetAsync(
            Guid draftId,
            CancellationToken cancellationToken) =>
            Task.FromResult<DocumentManagerEditorialDraft?>(null);

        public Task<DocumentManagerEditorialDraft> ApplyAsync(
            DocumentManagerEditorialDraftMutation mutation,
            CancellationToken cancellationToken)
        {
            Received = mutation;
            return Task.FromResult(
                new DocumentManagerEditorialDraft(
                    mutation.DraftId,
                    Guid.NewGuid(),
                    1,
                    new string('a', 64),
                    "book.pdf",
                    mutation.Title,
                    mutation.TitleOrigin,
                    mutation.PrimaryContributorName,
                    mutation.PrimaryContributorRole,
                    mutation.LanguageCode,
                    mutation.EditionStatement,
                    mutation.PublicationYear,
                    mutation.PublicationPlace,
                    mutation.Description,
                    mutation.TargetStatus,
                    mutation.ExpectedVersion + 1,
                    mutation.ActorUserId,
                    null,
                    null,
                    mutation.RejectionReason,
                    Now,
                    mutation.OccurredAtUtc,
                    []));
        }
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public UserId UserId { get; } = new(UserIdValue);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
