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
public sealed class DocumentManagerResultInboxTests
{
    [Fact]
    public async Task Inbox_persists_result_and_visuals_idempotently_and_rejects_conflicts()
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

        var resultReference = $"manager-result:integration-{Guid.NewGuid():N}";
        var received = CreateResult(resultReference);

        try
        {
            await using (var firstContext = new KnowledgeDbContext(options))
            {
                var inbox =
                    new PostgreSqlDocumentManagerResultInbox(firstContext);

                Assert.Equal(
                    DocumentManagerInboxWriteStatus.Stored,
                    await inbox.StoreAsync(
                        received,
                        CancellationToken.None));
            }

            await using (var replayContext = new KnowledgeDbContext(options))
            {
                var inbox =
                    new PostgreSqlDocumentManagerResultInbox(replayContext);

                Assert.Equal(
                    DocumentManagerInboxWriteStatus.AlreadyStored,
                    await inbox.StoreAsync(
                        received,
                        CancellationToken.None));
            }

            await using (var conflictContext = new KnowledgeDbContext(options))
            {
                var inbox =
                    new PostgreSqlDocumentManagerResultInbox(conflictContext);
                var conflicting =
                    received with
                    {
                        Payload = Encoding.UTF8.GetBytes("different payload")
                    };

                await Assert.ThrowsAsync<DocumentManagerResultIntegrityException>(
                    () => inbox.StoreAsync(
                        conflicting,
                        CancellationToken.None));
            }

            await using var connection =
                new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    (SELECT COUNT(*)
                       FROM document_manager_result_inbox
                      WHERE result_reference = $1),
                    (SELECT COUNT(*)
                       FROM document_manager_visual_asset_inbox
                      WHERE result_reference = $1),
                    (SELECT COUNT(*)
                       FROM document_manager_submission_manifest_inbox
                      WHERE submission_id = $2),
                    (SELECT COUNT(*)
                       FROM document_manager_expected_unit_inbox
                      WHERE submission_id = $2)
                """;
            command.Parameters.AddWithValue(resultReference);
            command.Parameters.AddWithValue(received.Claim.SubmissionId);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(1L, reader.GetInt64(1));
            Assert.Equal(1L, reader.GetInt64(2));
            Assert.Equal(1L, reader.GetInt64(3));
            await reader.CloseAsync();

            await using var assemblyContext =
                new KnowledgeDbContext(options);
            var assemblyReader =
                new PostgreSqlDocumentManagerSubmissionAssemblyReader(
                    assemblyContext);
            var assembly =
                await assemblyReader.GetAsync(
                    received.Claim.SubmissionId,
                    CancellationToken.None);

            Assert.NotNull(assembly);
            Assert.Equal(
                DocumentManagerSubmissionAssemblyStatus.Ready,
                assembly.Status);
            Assert.Equal(1, assembly.ReceivedPartCount);

            var draftCandidate =
                DocumentManagerEditorialDraftFactory.Create(
                    assembly,
                    received.ReceivedAtUtc.AddMinutes(1));

            await using (var draftContext = new KnowledgeDbContext(options))
            {
                var draftStore =
                    new PostgreSqlDocumentManagerEditorialDraftStore(
                        draftContext);
                var writeResult =
                    await draftStore.StoreAsync(
                        draftCandidate,
                        CancellationToken.None);

                Assert.Equal(
                    DocumentManagerEditorialDraftWriteStatus.Created,
                    writeResult.Status);
                Assert.Equal("book", writeResult.Draft.Title);
                Assert.Equal(
                    DocumentManagerEditorialDraftStatus.PendingReview,
                    writeResult.Draft.Status);
            }

            await using (var replayDraftContext = new KnowledgeDbContext(options))
            {
                var draftStore =
                    new PostgreSqlDocumentManagerEditorialDraftStore(
                        replayDraftContext);
                var writeResult =
                    await draftStore.StoreAsync(
                        draftCandidate,
                        CancellationToken.None);

                Assert.Equal(
                    DocumentManagerEditorialDraftWriteStatus.AlreadyExists,
                    writeResult.Status);
            }

            var editorId =
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var reviewTime = received.ReceivedAtUtc.AddMinutes(2);

            await using (var reviewContext = new KnowledgeDbContext(options))
            {
                var reviewStore =
                    new PostgreSqlDocumentManagerEditorialReviewStore(
                        reviewContext);
                var saved = await reviewStore.ApplyAsync(
                    ReviewMutation(
                        draftCandidate.Id,
                        expectedVersion: 0,
                        DocumentManagerEditorialReviewAction.Save,
                        DocumentManagerEditorialDraftStatus.InReview,
                        editorId,
                        reviewTime),
                    CancellationToken.None);

                Assert.Equal(1, saved.Version);
                Assert.Equal(
                    DocumentManagerEditorialDraftStatus.InReview,
                    saved.Status);
                Assert.Equal("Reviewed book", saved.Title);
            }

            await using (var staleContext = new KnowledgeDbContext(options))
            {
                var reviewStore =
                    new PostgreSqlDocumentManagerEditorialReviewStore(
                        staleContext);

                await Assert.ThrowsAsync<
                    DocumentManagerEditorialDraftConcurrencyException>(
                    () => reviewStore.ApplyAsync(
                        ReviewMutation(
                            draftCandidate.Id,
                            expectedVersion: 0,
                            DocumentManagerEditorialReviewAction.Save,
                            DocumentManagerEditorialDraftStatus.InReview,
                            editorId,
                            reviewTime),
                        CancellationToken.None));
            }

            await using (var approvalContext = new KnowledgeDbContext(options))
            {
                var reviewStore =
                    new PostgreSqlDocumentManagerEditorialReviewStore(
                        approvalContext);
                var approved = await reviewStore.ApplyAsync(
                    ReviewMutation(
                        draftCandidate.Id,
                        expectedVersion: 1,
                        DocumentManagerEditorialReviewAction.Approve,
                        DocumentManagerEditorialDraftStatus.Approved,
                        editorId,
                        reviewTime.AddMinutes(1)),
                    CancellationToken.None);

                Assert.Equal(2, approved.Version);
                Assert.Equal(
                    DocumentManagerEditorialDraftStatus.Approved,
                    approved.Status);
                Assert.Equal(editorId, approved.ReviewedByUserId);
                Assert.Equal(reviewTime.AddMinutes(1), approved.ReviewedAtUtc);
            }

            await using (var terminalContext = new KnowledgeDbContext(options))
            {
                var reviewStore =
                    new PostgreSqlDocumentManagerEditorialReviewStore(
                        terminalContext);

                await Assert.ThrowsAsync<
                    DocumentManagerEditorialReviewValidationException>(
                    () => reviewStore.ApplyAsync(
                        ReviewMutation(
                            draftCandidate.Id,
                            expectedVersion: 2,
                            DocumentManagerEditorialReviewAction.Save,
                            DocumentManagerEditorialDraftStatus.InReview,
                            editorId,
                            reviewTime.AddMinutes(2)),
                        CancellationToken.None));
            }

            await using var draftVerification =
                new NpgsqlConnection(connectionString);
            await draftVerification.OpenAsync();
            await using var draftVerificationCommand =
                draftVerification.CreateCommand();
            draftVerificationCommand.CommandText =
                """
                SELECT
                    (SELECT COUNT(*)
                       FROM document_manager_editorial_drafts
                      WHERE submission_id = $1),
                    (SELECT COUNT(*)
                       FROM document_manager_editorial_draft_parts
                      WHERE draft_id = $2)
                """;
            draftVerificationCommand.Parameters.AddWithValue(
                received.Claim.SubmissionId);
            draftVerificationCommand.Parameters.AddWithValue(
                draftCandidate.Id);

            await using var draftVerificationReader =
                await draftVerificationCommand.ExecuteReaderAsync();
            Assert.True(await draftVerificationReader.ReadAsync());
            Assert.Equal(1L, draftVerificationReader.GetInt64(0));
            Assert.Equal(1L, draftVerificationReader.GetInt64(1));
            await draftVerificationReader.CloseAsync();

            await using var reviewVerificationCommand =
                draftVerification.CreateCommand();
            reviewVerificationCommand.CommandText =
                """
                SELECT COUNT(*)
                FROM document_manager_editorial_review_events
                WHERE draft_id = $1
                """;
            reviewVerificationCommand.Parameters.AddWithValue(
                draftCandidate.Id);

            Assert.Equal(
                2L,
                (long)(await reviewVerificationCommand.ExecuteScalarAsync())!);
        }
        finally
        {
            await using var cleanup =
                new NpgsqlConnection(connectionString);
            await cleanup.OpenAsync();
            await using (var draftCommand = cleanup.CreateCommand())
            {
                draftCommand.CommandText =
                    """
                    DELETE FROM document_manager_editorial_drafts
                    WHERE submission_id = $1
                    """;
                draftCommand.Parameters.AddWithValue(
                    received.Claim.SubmissionId);
                await draftCommand.ExecuteNonQueryAsync();
            }

            await using (var resultCommand = cleanup.CreateCommand())
            {
                resultCommand.CommandText =
                    """
                    DELETE FROM document_manager_result_inbox
                    WHERE result_reference = $1
                    """;
                resultCommand.Parameters.AddWithValue(resultReference);
                await resultCommand.ExecuteNonQueryAsync();
            }

            await using var manifestCommand = cleanup.CreateCommand();
            manifestCommand.CommandText =
                """
                DELETE FROM document_manager_submission_manifest_inbox
                WHERE submission_id = $1
                """;
            manifestCommand.Parameters.AddWithValue(
                received.Claim.SubmissionId);
            await manifestCommand.ExecuteNonQueryAsync();
        }
    }

    private static DocumentManagerEditorialDraftMutation ReviewMutation(
        Guid draftId,
        int expectedVersion,
        DocumentManagerEditorialReviewAction action,
        DocumentManagerEditorialDraftStatus targetStatus,
        Guid editorId,
        DateTimeOffset occurredAtUtc) =>
        new(
            draftId,
            expectedVersion,
            action,
            "Reviewed book",
            "editorial",
            "Gary Habermas",
            "author",
            "en",
            "First edition",
            2026,
            "Paris",
            "Reviewed description",
            targetStatus,
            editorId,
            occurredAtUtc,
            null);

    private static ReceivedDocumentManagerResult CreateResult(
        string resultReference)
    {
        var payload =
            Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":\"document-processing-result-v4\"}");
        var visualPayload = Encoding.UTF8.GetBytes("visual");
        var availableAt =
            new DateTimeOffset(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);
        var submissionId = Guid.NewGuid();
        var processingUnitId = Guid.NewGuid();
        var scope =
            new DocumentManagerResultScope(
                "pageRange",
                1,
                50,
                "Part 1",
                null,
                null,
                null,
                null);
        var claim =
            new DocumentManagerResultClaim(
                resultReference,
                submissionId,
                processingUnitId,
                scope,
                "document-processing-result-v4",
                "application/vnd.document-processing-result+json",
                payload.LongLength,
                Sha256(payload),
                availableAt,
                Guid.NewGuid(),
                availableAt.AddMinutes(5),
                new DocumentManagerSubmissionManifest(
                    submissionId,
                    1,
                    new string('b', 64),
                    "book.pdf",
                    availableAt,
                    [
                        new DocumentManagerExpectedProcessingUnit(
                            processingUnitId,
                            1,
                            scope)
                    ]));
        var visualDescriptor =
            new DocumentManagerVisualAssetDescriptor(
                "visual-1",
                "image/png",
                visualPayload.LongLength,
                Sha256(visualPayload));

        return new ReceivedDocumentManagerResult(
            claim,
            payload,
            [
                new ReceivedDocumentManagerVisualAsset(
                    visualDescriptor,
                    visualPayload)
            ],
            availableAt.AddMinutes(1));
    }

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value))
            .ToLowerInvariant();
}
