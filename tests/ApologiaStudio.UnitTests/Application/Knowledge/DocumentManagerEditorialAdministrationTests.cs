using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

public sealed class DocumentManagerEditorialAdministrationTests
{
    private static readonly Guid DraftId =
        Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid SubmissionId =
        Guid.Parse("31000000-0000-0000-0000-000000000002");
    private static readonly Guid UserIdValue =
        Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 2, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reopen_restores_rejected_record_and_keeps_an_audited_mutation()
    {
        var store = new StubReviewStore(CreateRejectedDraft());
        var handler = new ReopenDocumentManagerEditorialDraftHandler(
            store,
            new StubAuthorizer(true),
            new StubCurrentUser(),
            new FixedTimeProvider(Now));

        await handler.HandleAsync(
            new ReopenDocumentManagerEditorialDraftCommand(DraftId, 4),
            CancellationToken.None);

        Assert.NotNull(store.Received);
        Assert.Equal(
            DocumentManagerEditorialReviewAction.Reopen,
            store.Received.Action);
        Assert.Equal(
            DocumentManagerEditorialDraftStatus.PendingReview,
            store.Received.TargetStatus);
        Assert.Null(store.Received.RejectionReason);
        Assert.Equal(UserIdValue, store.Received.ActorUserId);
        Assert.Equal(Now, store.Received.OccurredAtUtc);
    }

    [Fact]
    public async Task Reopen_is_refused_when_administration_is_disabled()
    {
        var handler = new ReopenDocumentManagerEditorialDraftHandler(
            new StubReviewStore(CreateRejectedDraft()),
            new StubAuthorizer(false),
            new StubCurrentUser(),
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<DocumentManagerAdministrationForbiddenException>(
            () => handler.HandleAsync(
                new ReopenDocumentManagerEditorialDraftCommand(DraftId, 4),
                CancellationToken.None));
    }

    [Fact]
    public async Task Purge_is_refused_when_administration_is_disabled()
    {
        var store = new StubAdministrationStore();
        var handler = new PurgeDocumentManagerSubmissionHandler(
            store,
            new StubAuthorizer(false));

        await Assert.ThrowsAsync<DocumentManagerAdministrationForbiddenException>(
            () => handler.HandleAsync(
                new PurgeDocumentManagerSubmissionCommand(DraftId, 4),
                CancellationToken.None));

        Assert.Null(store.Received);
    }

    private static DocumentManagerEditorialDraft CreateRejectedDraft() =>
        new(
            DraftId,
            SubmissionId,
            1,
            new string('a', 64),
            "book.pdf",
            "Book",
            "editorial",
            "Author",
            "author",
            "en",
            null,
            null,
            null,
            null,
            DocumentManagerEditorialDraftStatus.Rejected,
            4,
            UserIdValue,
            UserIdValue,
            Now.AddMinutes(-1),
            "Duplicate test import",
            Now.AddHours(-1),
            Now.AddMinutes(-1),
            [],
            []);

    private sealed class StubAuthorizer(bool isAuthorized)
        : IDocumentManagerAdministrationAuthorizer
    {
        public bool IsAuthorized { get; } = isAuthorized;
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public UserId UserId { get; } = new(UserIdValue);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubReviewStore(DocumentManagerEditorialDraft draft)
        : IDocumentManagerEditorialReviewStore
    {
        public DocumentManagerEditorialDraftMutation? Received { get; private set; }

        public Task<IReadOnlyList<DocumentManagerEditorialDraftSummary>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DocumentManagerEditorialDraftSummary>>([]);

        public Task<DocumentManagerEditorialDraft?> GetAsync(
            Guid draftId,
            CancellationToken cancellationToken) =>
            Task.FromResult<DocumentManagerEditorialDraft?>(draft);

        public Task<DocumentManagerEditorialDraft> ApplyAsync(
            DocumentManagerEditorialDraftMutation mutation,
            CancellationToken cancellationToken)
        {
            Received = mutation;
            return Task.FromResult(
                draft with
                {
                    Status = mutation.TargetStatus,
                    Version = mutation.ExpectedVersion + 1,
                    ReviewedByUserId = null,
                    ReviewedAtUtc = null,
                    RejectionReason = mutation.RejectionReason,
                    UpdatedAtUtc = mutation.OccurredAtUtc
                });
        }
    }

    private sealed class StubAdministrationStore
        : IDocumentManagerEditorialAdministrationStore
    {
        public PurgeDocumentManagerSubmissionCommand? Received { get; private set; }

        public Task<PurgedDocumentManagerSubmission> PurgeSubmissionAsync(
            PurgeDocumentManagerSubmissionCommand command,
            CancellationToken cancellationToken)
        {
            Received = command;
            return Task.FromResult(
                new PurgedDocumentManagerSubmission(
                    SubmissionId,
                    1,
                    4,
                    10,
                    1));
        }
    }
}
