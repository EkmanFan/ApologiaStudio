using ApologiaStudio.Application.Knowledge.DocumentProcessing;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

public sealed class DocumentManagerEditorialDraftTests
{
    [Fact]
    public void Factory_creates_a_pending_record_with_ordered_source_parts()
    {
        var assembly = CreateAssembly(
            DocumentManagerSubmissionAssemblyStatus.Ready,
            receivedPartCount: 2);
        var createdAt =
            new DateTimeOffset(2026, 9, 2, 18, 0, 0, TimeSpan.Zero);

        var draft =
            DocumentManagerEditorialDraftFactory.Create(
                assembly,
                createdAt);
        var replay =
            DocumentManagerEditorialDraftFactory.Create(
                assembly,
                createdAt.AddHours(1));

        Assert.Equal(draft.Id, replay.Id);
        Assert.Equal("The Case for the Resurrection of Jesus", draft.Title);
        Assert.Equal("original_filename", draft.TitleOrigin);
        Assert.Equal(
            DocumentManagerEditorialDraftStatus.PendingReview,
            draft.Status);
        Assert.Null(draft.LanguageCode);
        Assert.Null(draft.EditionStatement);
        Assert.Null(draft.PublicationYear);
        Assert.Null(draft.PublicationPlace);
        Assert.Null(draft.Description);
        Assert.Equal(createdAt, draft.CreatedAtUtc);
        Assert.Equal([1, 2], draft.Parts.Select(part => part.Ordinal));
        Assert.Equal(
            ["manager-result:1", "manager-result:2"],
            draft.Parts.Select(part => part.ResultReference));
    }

    [Fact]
    public void Factory_rejects_an_incomplete_assembly()
    {
        var assembly = CreateAssembly(
            DocumentManagerSubmissionAssemblyStatus.AwaitingParts,
            receivedPartCount: 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => DocumentManagerEditorialDraftFactory.Create(
                assembly,
                DateTimeOffset.UtcNow));

        Assert.Contains("complete", exception.Message);
    }

    [Fact]
    public async Task Handler_does_not_create_a_record_while_parts_are_missing()
    {
        var assembly = CreateAssembly(
            DocumentManagerSubmissionAssemblyStatus.AwaitingParts,
            receivedPartCount: 1);
        var store = new StubDraftStore();
        var handler =
            new PrepareDocumentManagerEditorialDraftHandler(
                new StubAssemblyReader(assembly),
                store,
                TimeProvider.System);

        var result =
            await handler.PrepareAsync(
                assembly.SubmissionId,
                CancellationToken.None);

        Assert.Equal(
            DocumentManagerEditorialDraftPreparationStatus.AwaitingParts,
            result.Status);
        Assert.Null(result.Draft);
        Assert.Null(store.Received);
    }

    [Fact]
    public async Task Handler_creates_one_record_when_the_assembly_is_ready()
    {
        var assembly = CreateAssembly(
            DocumentManagerSubmissionAssemblyStatus.Ready,
            receivedPartCount: 2);
        var createdAt =
            new DateTimeOffset(2026, 9, 2, 18, 0, 0, TimeSpan.Zero);
        var store = new StubDraftStore();
        var handler =
            new PrepareDocumentManagerEditorialDraftHandler(
                new StubAssemblyReader(assembly),
                store,
                new FixedTimeProvider(createdAt));

        var result =
            await handler.PrepareAsync(
                assembly.SubmissionId,
                CancellationToken.None);

        Assert.Equal(
            DocumentManagerEditorialDraftPreparationStatus.Created,
            result.Status);
        Assert.Same(store.Received, result.Draft);
        Assert.Equal(createdAt, result.Draft?.CreatedAtUtc);
    }

    private static DocumentManagerSubmissionAssembly CreateAssembly(
        DocumentManagerSubmissionAssemblyStatus status,
        int receivedPartCount)
    {
        var firstId =
            Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondId =
            Guid.Parse("10000000-0000-0000-0000-000000000002");

        return new DocumentManagerSubmissionAssembly(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            2,
            new string('a', 64),
            "The Case for the Resurrection of Jesus.pdf",
            status,
            [
                new DocumentManagerSubmissionPart(
                    secondId,
                    2,
                    PageRange(51, 100, "Part 2"),
                    receivedPartCount >= 2 ? "manager-result:2" : null),
                new DocumentManagerSubmissionPart(
                    firstId,
                    1,
                    PageRange(1, 50, "Part 1"),
                    "manager-result:1")
            ],
            []);
    }

    private static DocumentManagerResultScope PageRange(
        int start,
        int end,
        string title) =>
        new(
            "pageRange",
            start,
            end,
            title,
            null,
            null,
            null,
            null);

    private sealed class StubAssemblyReader(
        DocumentManagerSubmissionAssembly assembly)
        : IDocumentManagerSubmissionAssemblyReader
    {
        public Task<DocumentManagerSubmissionAssembly?> GetAsync(
            Guid submissionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<DocumentManagerSubmissionAssembly?>(assembly);
    }

    private sealed class StubDraftStore : IDocumentManagerEditorialDraftStore
    {
        public DocumentManagerEditorialDraft? Received { get; private set; }

        public Task<DocumentManagerEditorialDraftWriteResult> StoreAsync(
            DocumentManagerEditorialDraft draft,
            CancellationToken cancellationToken)
        {
            Received = draft;
            return Task.FromResult(
                new DocumentManagerEditorialDraftWriteResult(
                    DocumentManagerEditorialDraftWriteStatus.Created,
                    draft));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
