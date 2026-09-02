using System.Security.Cryptography;
using System.Text;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

public sealed class ConsumeDocumentManagerResultHandlerTests
{
    [Fact]
    public async Task Handler_stores_verified_content_and_visuals_before_acknowledging()
    {
        var events = new List<string>();
        var payload = Utf8("{\"schemaVersion\":\"document-processing-result-v4\"}");
        var visualPayload = Utf8("visual-bytes");
        var descriptor =
            new DocumentManagerVisualAssetDescriptor(
                "asset-1",
                "image/png",
                visualPayload.LongLength,
                Sha256(visualPayload));
        var source =
            new StubResultSource(
                CreateClaim(payload),
                payload,
                [descriptor],
                new Dictionary<string, byte[]>
                {
                    [descriptor.AssetId] = visualPayload
                },
                events);
        var inbox =
            new StubResultInbox(
                DocumentManagerInboxWriteStatus.Stored,
                events);
        var receivedAt =
            new DateTimeOffset(2026, 9, 2, 15, 0, 0, TimeSpan.Zero);
        var handler =
            new ConsumeDocumentManagerResultHandler(
                source,
                inbox,
                new StubDraftPreparer(events),
                new FixedTimeProvider(receivedAt));

        var result =
            await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(
            DocumentManagerConsumeStatus.StoredAndAcknowledged,
            result.Status);
        Assert.Equal("manager-result:1", result.ResultReference);
        Assert.Equal(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            result.SubmissionId);
        Assert.Equal(
            ["claim", "content", "visuals", "visual:asset-1", "store", "draft", "ack"],
            events);
        Assert.Equal(
            DocumentManagerEditorialDraftPreparationStatus.Created,
            result.DraftPreparation?.Status);

        var stored = Assert.IsType<ReceivedDocumentManagerResult>(inbox.Stored);
        Assert.Equal(receivedAt, stored.ReceivedAtUtc);
        Assert.Equal(payload, stored.Payload);
        Assert.Equal(visualPayload, Assert.Single(stored.VisualAssets).Payload);
    }

    [Fact]
    public async Task Handler_returns_without_storage_when_no_result_is_available()
    {
        var events = new List<string>();
        var source =
            new StubResultSource(
                null,
                [],
                [],
                new Dictionary<string, byte[]>(),
                events);
        var inbox =
            new StubResultInbox(
                DocumentManagerInboxWriteStatus.Stored,
                events);
        var handler =
            new ConsumeDocumentManagerResultHandler(
                source,
                inbox,
                new StubDraftPreparer(events),
                TimeProvider.System);

        var result =
            await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(
            DocumentManagerConsumeStatus.NoResultAvailable,
            result.Status);
        Assert.Null(result.ResultReference);
        Assert.Null(result.SubmissionId);
        Assert.Null(result.DraftPreparation);
        Assert.Equal(["claim"], events);
        Assert.Null(inbox.Stored);
    }

    [Fact]
    public async Task Handler_acknowledges_an_exact_replay_after_idempotent_storage_check()
    {
        var events = new List<string>();
        var payload = Utf8("{\"schemaVersion\":\"document-processing-result-v4\"}");
        var source =
            new StubResultSource(
                CreateClaim(payload),
                payload,
                [],
                new Dictionary<string, byte[]>(),
                events);
        var inbox =
            new StubResultInbox(
                DocumentManagerInboxWriteStatus.AlreadyStored,
                events);
        var handler =
            new ConsumeDocumentManagerResultHandler(
                source,
                inbox,
                new StubDraftPreparer(events),
                TimeProvider.System);

        var result =
            await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(
            DocumentManagerConsumeStatus.AlreadyStoredAndAcknowledged,
            result.Status);
        Assert.Equal("ack", events[^1]);
    }

    [Fact]
    public async Task Handler_does_not_store_or_ack_tampered_content()
    {
        var events = new List<string>();
        var advertised = Utf8("{\"schemaVersion\":\"document-processing-result-v4\"}");
        var source =
            new StubResultSource(
                CreateClaim(advertised),
                Utf8("{\"schemaVersion\":\"tampered-result\"}"),
                [],
                new Dictionary<string, byte[]>(),
                events);
        var inbox =
            new StubResultInbox(
                DocumentManagerInboxWriteStatus.Stored,
                events);
        var handler =
            new ConsumeDocumentManagerResultHandler(
                source,
                inbox,
                new StubDraftPreparer(events),
                TimeProvider.System);

        await Assert.ThrowsAsync<DocumentManagerResultIntegrityException>(
            () => handler.HandleAsync(CancellationToken.None));

        Assert.DoesNotContain("store", events);
        Assert.DoesNotContain("ack", events);
    }

    [Fact]
    public async Task Handler_does_not_store_or_ack_a_tampered_visual()
    {
        var events = new List<string>();
        var payload = Utf8("{\"schemaVersion\":\"document-processing-result-v4\"}");
        var advertisedVisual = Utf8("expected");
        var descriptor =
            new DocumentManagerVisualAssetDescriptor(
                "asset-1",
                "image/png",
                advertisedVisual.LongLength,
                Sha256(advertisedVisual));
        var source =
            new StubResultSource(
                CreateClaim(payload),
                payload,
                [descriptor],
                new Dictionary<string, byte[]>
                {
                    [descriptor.AssetId] = Utf8("tampered")
                },
                events);
        var inbox =
            new StubResultInbox(
                DocumentManagerInboxWriteStatus.Stored,
                events);
        var handler =
            new ConsumeDocumentManagerResultHandler(
                source,
                inbox,
                new StubDraftPreparer(events),
                TimeProvider.System);

        await Assert.ThrowsAsync<DocumentManagerResultIntegrityException>(
            () => handler.HandleAsync(CancellationToken.None));

        Assert.DoesNotContain("store", events);
        Assert.DoesNotContain("ack", events);
    }

    [Fact]
    public async Task Handler_leaves_stored_result_replayable_when_ack_fails()
    {
        var events = new List<string>();
        var payload = Utf8("{\"schemaVersion\":\"document-processing-result-v4\"}");
        var source =
            new StubResultSource(
                CreateClaim(payload),
                payload,
                [],
                new Dictionary<string, byte[]>(),
                events)
            {
                AckException = new HttpRequestException("ack failed")
            };
        var inbox =
            new StubResultInbox(
                DocumentManagerInboxWriteStatus.Stored,
                events);
        var handler =
            new ConsumeDocumentManagerResultHandler(
                source,
                inbox,
                new StubDraftPreparer(events),
                TimeProvider.System);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => handler.HandleAsync(CancellationToken.None));

        Assert.Equal("draft", events[^2]);
        Assert.Equal("ack", events[^1]);
        Assert.NotNull(inbox.Stored);
    }

    [Fact]
    public async Task Handler_does_not_ack_when_provisional_record_preparation_fails()
    {
        var events = new List<string>();
        var payload = Utf8("{\"schemaVersion\":\"document-processing-result-v4\"}");
        var source =
            new StubResultSource(
                CreateClaim(payload),
                payload,
                [],
                new Dictionary<string, byte[]>(),
                events);
        var inbox =
            new StubResultInbox(
                DocumentManagerInboxWriteStatus.Stored,
                events);
        var draftPreparer =
            new StubDraftPreparer(events)
            {
                Exception = new InvalidOperationException("draft failed")
            };
        var handler =
            new ConsumeDocumentManagerResultHandler(
                source,
                inbox,
                draftPreparer,
                TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(CancellationToken.None));

        Assert.Equal("store", events[^2]);
        Assert.Equal("draft", events[^1]);
        Assert.DoesNotContain("ack", events);
    }

    private static DocumentManagerResultClaim CreateClaim(byte[] payload)
    {
        var availableAt =
            new DateTimeOffset(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);

        var submissionId =
            Guid.Parse("00000000-0000-0000-0000-000000000001");
        var processingUnitId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");
        var scope =
            new DocumentManagerResultScope(
                "wholeDocument",
                null,
                null,
                null,
                null,
                null,
                null,
                null);

        return new DocumentManagerResultClaim(
            "manager-result:1",
            submissionId,
            processingUnitId,
            scope,
            "document-processing-result-v4",
            "application/vnd.document-processing-result+json",
            payload.LongLength,
            Sha256(payload),
            availableAt,
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            availableAt.AddMinutes(5),
            new DocumentManagerSubmissionManifest(
                submissionId,
                1,
                new string('a', 64),
                "book.pdf",
                availableAt,
                [
                    new DocumentManagerExpectedProcessingUnit(
                        processingUnitId,
                        1,
                        scope)
                ]));
    }

    private static byte[] Utf8(string value) =>
        Encoding.UTF8.GetBytes(value);

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value))
            .ToLowerInvariant();

    private sealed class StubResultSource(
        DocumentManagerResultClaim? claim,
        byte[] payload,
        IReadOnlyList<DocumentManagerVisualAssetDescriptor> descriptors,
        IReadOnlyDictionary<string, byte[]> visualPayloads,
        ICollection<string> events)
        : IDocumentManagerResultSource
    {
        public Exception? AckException { get; init; }

        public Task<DocumentManagerResultClaim?> ClaimNextAsync(
            CancellationToken cancellationToken)
        {
            events.Add("claim");
            return Task.FromResult(claim);
        }

        public Task<byte[]> ReadContentAsync(
            DocumentManagerResultClaim resultClaim,
            CancellationToken cancellationToken)
        {
            events.Add("content");
            return Task.FromResult(payload);
        }

        public Task<IReadOnlyList<DocumentManagerVisualAssetDescriptor>>
            ListVisualAssetsAsync(
                DocumentManagerResultClaim resultClaim,
                CancellationToken cancellationToken)
        {
            events.Add("visuals");
            return Task.FromResult(descriptors);
        }

        public Task<byte[]> ReadVisualAssetAsync(
            DocumentManagerResultClaim resultClaim,
            DocumentManagerVisualAssetDescriptor visualAsset,
            CancellationToken cancellationToken)
        {
            events.Add($"visual:{visualAsset.AssetId}");
            return Task.FromResult(visualPayloads[visualAsset.AssetId]);
        }

        public Task AcknowledgeAsync(
            DocumentManagerResultClaim resultClaim,
            CancellationToken cancellationToken)
        {
            events.Add("ack");
            return AckException is null
                ? Task.CompletedTask
                : Task.FromException(AckException);
        }
    }

    private sealed class StubResultInbox(
        DocumentManagerInboxWriteStatus status,
        ICollection<string> events)
        : IDocumentManagerResultInbox
    {
        public ReceivedDocumentManagerResult? Stored { get; private set; }

        public Task<DocumentManagerInboxWriteStatus> StoreAsync(
            ReceivedDocumentManagerResult result,
            CancellationToken cancellationToken)
        {
            events.Add("store");
            Stored = result;
            return Task.FromResult(status);
        }
    }

    private sealed class StubDraftPreparer(ICollection<string> events)
        : IDocumentManagerEditorialDraftPreparer
    {
        public Exception? Exception { get; init; }

        public Task<DocumentManagerEditorialDraftPreparationResult> PrepareAsync(
            Guid submissionId,
            CancellationToken cancellationToken)
        {
            events.Add("draft");

            if (Exception is not null)
            {
                return Task.FromException<DocumentManagerEditorialDraftPreparationResult>(
                    Exception);
            }

            var processingUnitId =
                Guid.Parse("00000000-0000-0000-0000-000000000002");
            var scope =
                new DocumentManagerResultScope(
                    "wholeDocument",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            var assembly =
                new DocumentManagerSubmissionAssembly(
                    submissionId,
                    1,
                    new string('a', 64),
                    "book.pdf",
                    DocumentManagerSubmissionAssemblyStatus.Ready,
                    [
                        new DocumentManagerSubmissionPart(
                            processingUnitId,
                            1,
                            scope,
                            "manager-result:1")
                    ],
                    []);
            var draft =
                DocumentManagerEditorialDraftFactory.Create(
                    assembly,
                    new DateTimeOffset(
                        2026,
                        9,
                        2,
                        15,
                        0,
                        0,
                        TimeSpan.Zero));

            return Task.FromResult(
                new DocumentManagerEditorialDraftPreparationResult(
                    DocumentManagerEditorialDraftPreparationStatus.Created,
                    assembly,
                    draft));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
