using System.Security.Cryptography;
using System.Text;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class DocumentManagerEditorialAdministrationTests
{
    [Fact]
    public async Task Reopen_is_audited_and_purge_removes_the_complete_submission()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options =
            new DbContextOptionsBuilder<KnowledgeDbContext>()
                .UseNpgsql(
                    connectionString,
                    builder => builder.UseVector())
                .Options;

        await using (var migrationContext = new KnowledgeDbContext(options))
        {
            await migrationContext.Database.MigrateAsync();
        }

        var submissionId = Guid.NewGuid();
        var processingUnitIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var resultReferences = new[]
        {
            $"manager-result:admin-{Guid.NewGuid():N}",
            $"manager-result:admin-{Guid.NewGuid():N}"
        };
        var editorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var now = new DateTimeOffset(2026, 9, 2, 20, 0, 0, TimeSpan.Zero);

        try
        {
            var rejected = await SeedRejectedSubmissionAsync(
                options,
                submissionId,
                processingUnitIds,
                resultReferences,
                editorId,
                now);

            await using (var reopenContext = new KnowledgeDbContext(options))
            {
                var reviewStore =
                    new PostgreSqlDocumentManagerEditorialReviewStore(
                        reopenContext);
                var reopened = await reviewStore.ApplyAsync(
                    ReviewMutation(
                        rejected.Id,
                        expectedVersion: 1,
                        DocumentManagerEditorialReviewAction.Reopen,
                        DocumentManagerEditorialDraftStatus.PendingReview,
                        editorId,
                        now.AddMinutes(1),
                        rejectionReason: null),
                    CancellationToken.None);

                Assert.Equal(2, reopened.Version);
                Assert.Equal(
                    DocumentManagerEditorialDraftStatus.PendingReview,
                    reopened.Status);
                Assert.Null(reopened.RejectionReason);
                Assert.Null(reopened.ReviewedAtUtc);
                Assert.Null(reopened.ReviewedByUserId);
            }

            Assert.Equal(
                new[] { "reject", "reopen" },
                await ReadReviewActionsAsync(
                    connectionString,
                    rejected.Id));

            PurgedDocumentManagerSubmission purgeResult;
            await using (var purgeContext = new KnowledgeDbContext(options))
            {
                var store =
                    new PostgreSqlDocumentManagerEditorialAdministrationStore(
                        purgeContext);
                purgeResult = await store.PurgeSubmissionAsync(
                    new PurgeDocumentManagerSubmissionCommand(rejected.Id, 2),
                    CancellationToken.None);
            }

            Assert.Equal(submissionId, purgeResult.SubmissionId);
            Assert.Equal(1, purgeResult.DeletedDraftCount);
            Assert.Equal(2, purgeResult.DeletedResultCount);
            Assert.Equal(2, purgeResult.DeletedVisualAssetCount);
            Assert.Equal(1, purgeResult.DeletedManifestCount);

            await AssertSubmissionWasPurgedAsync(
                connectionString,
                submissionId,
                rejected.Id,
                resultReferences);
        }
        finally
        {
            await CleanupAsync(connectionString, submissionId);
        }
    }

    private static async Task<DocumentManagerEditorialDraft>
        SeedRejectedSubmissionAsync(
            DbContextOptions<KnowledgeDbContext> options,
            Guid submissionId,
            IReadOnlyList<Guid> processingUnitIds,
            IReadOnlyList<string> resultReferences,
            Guid editorId,
            DateTimeOffset now)
    {
        var scopes = new[]
        {
            new DocumentManagerResultScope(
                "pageRange", 1, 10, "Part 1", null, null, null, null),
            new DocumentManagerResultScope(
                "pageRange", 11, 20, "Part 2", null, null, null, null)
        };
        var manifest = new DocumentManagerSubmissionManifest(
            submissionId,
            1,
            new string('a', 64),
            "administrative-test.pdf",
            now.AddMinutes(-10),
            processingUnitIds
                .Select(
                    (unitId, index) =>
                        new DocumentManagerExpectedProcessingUnit(
                            unitId,
                            index + 1,
                            scopes[index]))
                .ToArray());

        for (var index = 0; index < processingUnitIds.Count; index++)
        {
            var received = CreateResult(
                resultReferences[index],
                submissionId,
                processingUnitIds[index],
                scopes[index],
                manifest,
                now.AddMinutes(-5 + index));

            await using var inboxContext = new KnowledgeDbContext(options);
            var inbox = new PostgreSqlDocumentManagerResultInbox(inboxContext);
            Assert.Equal(
                DocumentManagerInboxWriteStatus.Stored,
                await inbox.StoreAsync(received, CancellationToken.None));
        }

        DocumentManagerSubmissionAssembly assembly;
        await using (var assemblyContext = new KnowledgeDbContext(options))
        {
            var reader =
                new PostgreSqlDocumentManagerSubmissionAssemblyReader(
                    assemblyContext);
            assembly = await reader.GetAsync(
                submissionId,
                CancellationToken.None) ?? throw new InvalidOperationException(
                "The seeded submission was not assembled.");
        }

        var candidate = DocumentManagerEditorialDraftFactory.Create(
            assembly,
            now.AddMinutes(-2));
        DocumentManagerEditorialDraft draft;
        await using (var draftContext = new KnowledgeDbContext(options))
        {
            var store =
                new PostgreSqlDocumentManagerEditorialDraftStore(draftContext);
            draft = (await store.StoreAsync(
                candidate,
                CancellationToken.None)).Draft;
        }

        await using var reviewContext = new KnowledgeDbContext(options);
        var reviewStore =
            new PostgreSqlDocumentManagerEditorialReviewStore(reviewContext);
        return await reviewStore.ApplyAsync(
            ReviewMutation(
                draft.Id,
                expectedVersion: 0,
                DocumentManagerEditorialReviewAction.Reject,
                DocumentManagerEditorialDraftStatus.Rejected,
                editorId,
                now,
                "Test rejection"),
            CancellationToken.None);
    }

    private static ReceivedDocumentManagerResult CreateResult(
        string resultReference,
        Guid submissionId,
        Guid processingUnitId,
        DocumentManagerResultScope scope,
        DocumentManagerSubmissionManifest manifest,
        DateTimeOffset availableAtUtc)
    {
        var payload = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":\"document-processing-result-v4\"}");
        var visualPayload = Encoding.UTF8.GetBytes("visual");
        var claim = new DocumentManagerResultClaim(
            resultReference,
            submissionId,
            processingUnitId,
            scope,
            "document-processing-result-v4",
            "application/vnd.document-processing-result+json",
            payload.LongLength,
            Sha256(payload),
            availableAtUtc,
            Guid.NewGuid(),
            availableAtUtc.AddMinutes(5),
            manifest);
        var descriptor = new DocumentManagerVisualAssetDescriptor(
            $"visual-{processingUnitId:N}",
            "image/png",
            visualPayload.LongLength,
            Sha256(visualPayload));

        return new ReceivedDocumentManagerResult(
            claim,
            payload,
            [new ReceivedDocumentManagerVisualAsset(descriptor, visualPayload)],
            availableAtUtc.AddSeconds(30));
    }

    private static DocumentManagerEditorialDraftMutation ReviewMutation(
        Guid draftId,
        int expectedVersion,
        DocumentManagerEditorialReviewAction action,
        DocumentManagerEditorialDraftStatus targetStatus,
        Guid editorId,
        DateTimeOffset occurredAtUtc,
        string? rejectionReason) =>
        new(
            draftId,
            expectedVersion,
            action,
            "Administrative test book",
            "editorial",
            "Test Author",
            "author",
            "en",
            null,
            null,
            null,
            null,
            targetStatus,
            editorId,
            occurredAtUtc,
            rejectionReason);

    private static async Task<string[]> ReadReviewActionsAsync(
        string connectionString,
        Guid draftId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT action
            FROM document_manager_editorial_review_events
            WHERE draft_id = $1
            ORDER BY version
            """;
        command.Parameters.AddWithValue(draftId);
        await using var reader = await command.ExecuteReaderAsync();
        var actions = new List<string>();
        while (await reader.ReadAsync())
        {
            actions.Add(reader.GetString(0));
        }

        return actions.ToArray();
    }

    private static async Task AssertSubmissionWasPurgedAsync(
        string connectionString,
        Guid submissionId,
        Guid draftId,
        IReadOnlyList<string> resultReferences)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                (SELECT COUNT(*) FROM document_manager_editorial_drafts WHERE submission_id = $1),
                (SELECT COUNT(*) FROM document_manager_editorial_draft_parts WHERE draft_id = $2),
                (SELECT COUNT(*) FROM document_manager_editorial_review_events WHERE draft_id = $2),
                (SELECT COUNT(*) FROM document_manager_result_inbox WHERE submission_id = $1),
                (SELECT COUNT(*) FROM document_manager_visual_asset_inbox WHERE result_reference = ANY($3)),
                (SELECT COUNT(*) FROM document_manager_submission_manifest_inbox WHERE submission_id = $1),
                (SELECT COUNT(*) FROM document_manager_expected_unit_inbox WHERE submission_id = $1)
            """;
        command.Parameters.AddWithValue(submissionId);
        command.Parameters.AddWithValue(draftId);
        command.Parameters.AddWithValue(resultReferences.ToArray());

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        for (var ordinal = 0; ordinal < 7; ordinal++)
        {
            Assert.Equal(0L, reader.GetInt64(ordinal));
        }
    }

    private static async Task CleanupAsync(
        string connectionString,
        Guid submissionId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        foreach (var table in new[]
                 {
                     "document_manager_editorial_drafts",
                     "document_manager_result_inbox",
                     "document_manager_submission_manifest_inbox"
                 })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                $"DELETE FROM {table} WHERE submission_id = $1";
            command.Parameters.AddWithValue(submissionId);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
