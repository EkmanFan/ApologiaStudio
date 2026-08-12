using ApologiaStudio.Application.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

public sealed class GenericKnowledgeImportStoreTests
{
    [Fact]
    public async Task GenericImportStore_ShouldPersistProjectAndRemovePackage()
    {
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        var package = CreatePackage();
        var profile = new KnowledgeRetrievalProfile(
            "integration-retrieval-v1",
            "segment-character-window",
            "v1",
            1_000,
            100,
            100,
            200,
            "integration-test",
            "integration-test-model",
            3);
        var chunks = KnowledgeRetrievalChunkBuilder.Build(
            package,
            profile);
        var embeddings = chunks
            .Select(_ => new[] { 1f, 0f, 0f })
            .ToArray();
        var modelDigest = new string('b', 64);
        var connectionString =
            KnowledgeStoreTestConnection.Resolve();

        try
        {
            var created =
                await PostgreSqlKnowledgeImportStore.ImportAsync(
                    connectionString,
                    package,
                    CancellationToken.None);

            Assert.True(created.WasCreated);
            Assert.Equal(
                package.PrimaryWorkId,
                created.WorkId);
            Assert.Equal(
                package.NormalizedArtifactId,
                created.NormalizedArtifactId);
            Assert.Equal(
                package.Segments.Count,
                created.SegmentCount);

            var secondImport =
                await PostgreSqlKnowledgeImportStore.ImportAsync(
                    connectionString,
                    package,
                    CancellationToken.None);

            Assert.False(secondImport.WasCreated);

            var projection =
                await PostgreSqlKnowledgeRetrievalProjectionStore.ProjectAsync(
                    connectionString,
                    package,
                    profile,
                    chunks,
                    modelDigest,
                    embeddings,
                    CancellationToken.None);

            Assert.True(projection.WasCreated);
            Assert.Equal(
                chunks.Count,
                projection.ChunkCount);

            Assert.True(
                await PostgreSqlKnowledgeRetrievalProjectionStore.ExistsAndMatchesAsync(
                    connectionString,
                    package,
                    profile,
                    chunks,
                    modelDigest,
                    CancellationToken.None));

            await AssertPersistedAsync(
                connectionString,
                package,
                profile,
                chunks.Count);
        }
        finally
        {
            var deletableHashes =
                await PostgreSqlKnowledgeImportStore.RemoveAsync(
                    connectionString,
                    package,
                    CancellationToken.None);

            Assert.Contains(
                package.Artifacts[0].Sha256,
                deletableHashes);
            Assert.Contains(
                package.Artifacts[1].Sha256,
                deletableHashes);
        }

        await AssertRemovedAsync(
            connectionString,
            package.PrimaryWorkId);
    }

    private static async Task AssertPersistedAsync(
        string connectionString,
        KnowledgeImportPackage package,
        KnowledgeRetrievalProfile profile,
        int expectedChunkCount)
    {
        await using var connection =
            new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                w.title,
                s.segment_kind,
                COUNT(c.id)
            FROM knowledge_works w
            JOIN knowledge_expressions e
              ON e.work_id = w.id
            JOIN knowledge_manifestations m
              ON m.expression_id = e.id
            JOIN knowledge_artifacts a
              ON a.manifestation_id = m.id
            JOIN knowledge_document_segments s
              ON s.artifact_id = a.id
            LEFT JOIN knowledge_retrieval_chunks c
              ON c.artifact_id = a.id
             AND c.chunking_strategy = @strategy
             AND c.chunking_version = @version
            WHERE w.id = @work_id
              AND a.id = @artifact_id
            GROUP BY w.title, s.segment_kind
            """,
            connection);

        command.Parameters.AddWithValue(
            "strategy",
            profile.ChunkingStrategy);
        command.Parameters.AddWithValue(
            "version",
            profile.ChunkingVersion);
        command.Parameters.AddWithValue(
            "work_id",
            package.PrimaryWorkId);
        command.Parameters.AddWithValue(
            "artifact_id",
            package.NormalizedArtifactId);

        await using var reader =
            await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(
            "Generic import integration fixture",
            reader.GetString(0));
        Assert.Equal(
            "main_text",
            reader.GetString(1));
        Assert.Equal(
            expectedChunkCount,
            checked((int)reader.GetInt64(2)));
        Assert.False(await reader.ReadAsync());
    }

    private static async Task AssertRemovedAsync(
        string connectionString,
        Guid primaryWorkId)
    {
        await using var connection =
            new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM knowledge_resources
                WHERE id = @id)
            """,
            connection);

        command.Parameters.AddWithValue(
            "id",
            primaryWorkId);

        Assert.False(
            Assert.IsType<bool>(
                await command.ExecuteScalarAsync()));
    }

    private static KnowledgeImportPackage CreatePackage()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var stableNamespace =
            "integration-" + suffix;
        var workId =
            KnowledgeStableIds.ForSourceProfile(
                stableNamespace,
                "work");
        var expressionId =
            KnowledgeStableIds.ForSourceProfile(
                stableNamespace,
                "expression");
        var manifestationId =
            KnowledgeStableIds.ForSourceProfile(
                stableNamespace,
                "manifestation");
        var rawArtifactId =
            KnowledgeStableIds.ForSourceProfile(
                stableNamespace,
                "raw-artifact");
        var normalizedArtifactId =
            KnowledgeStableIds.ForSourceProfile(
                stableNamespace,
                "normalized-artifact");
        var segmentId =
            KnowledgeStableIds.ForSourceProfile(
                stableNamespace,
                "segment-0");
        var classificationCode =
            "integration_fixture_" + suffix;
        byte[] normalizedBytes = [1, 2, 3];

        return new KnowledgeImportPackage(
            "integration-import-" + suffix,
            stableNamespace,
            workId,
            normalizedArtifactId,
            "integration-test",
            [
                new KnowledgeImportWork(
                    workId,
                    "approved",
                    "Generic import integration fixture",
                    "en",
                    null)
            ],
            [
                new KnowledgeImportExpression(
                    expressionId,
                    "approved",
                    workId,
                    "en",
                    "Fixture expression",
                    null)
            ],
            Array.Empty<KnowledgeImportExpressionRelation>(),
            [
                new KnowledgeImportManifestation(
                    manifestationId,
                    "approved",
                    expressionId,
                    "Fixture edition",
                    2026,
                    null,
                    "Generic import fixture")
            ],
            Array.Empty<KnowledgeImportManifestationIdentifier>(),
            Array.Empty<KnowledgeImportContributor>(),
            Array.Empty<KnowledgeImportContribution>(),
            [
                new KnowledgeImportArtifact(
                    rawArtifactId,
                    "approved",
                    manifestationId,
                    null,
                    "raw",
                    new string('c', 64),
                    "application/pdf",
                    10,
                    "https://example.invalid/fixture.pdf",
                    "active",
                    ".pdf",
                    "/tmp/integration-source.pdf",
                    null),
                new KnowledgeImportArtifact(
                    normalizedArtifactId,
                    "approved",
                    manifestationId,
                    rawArtifactId,
                    "normalized",
                    "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81",
                    "text/plain; charset=utf-8",
                    normalizedBytes.LongLength,
                    null,
                    "active",
                    ".txt",
                    null,
                    normalizedBytes)
            ],
            [
                new KnowledgeImportProcessingActivity(
                    rawArtifactId,
                    normalizedArtifactId,
                    "normalize",
                    "integration-test",
                    "v1",
                    "{\"fixture\":true}",
                    "integration-test",
                    "completed")
            ],
            [
                new KnowledgeImportSegment(
                    segmentId,
                    "approved",
                    normalizedArtifactId,
                    null,
                    DocumentSegmentType.ParagraphGroup,
                    DocumentSegmentKind.MainText,
                    0,
                    null,
                    "Representative generic evidence text.",
                    "page 1")
            ],
            [
                new KnowledgeImportClassificationTerm(
                    KnowledgeClassificationDimension.SourceKind,
                    classificationCode,
                    "Integration fixture source",
                    null,
                    null)
            ],
            [
                new KnowledgeImportClassificationAssertion(
                    KnowledgeStableIds.ForSourceProfile(
                        stableNamespace,
                        "source-kind-assertion"),
                    workId,
                    KnowledgeClassificationDimension.SourceKind,
                    classificationCode,
                    null,
                    "editorial",
                    "integration-test",
                    "verified",
                    "integration-test",
                    "Integration fixture assertion.",
                    segmentId,
                    null)
            ],
            [
                new KnowledgeImportMetadataAssertion(
                    KnowledgeStableIds.ForSourceProfile(
                        stableNamespace,
                        "metadata-assertion"),
                    rawArtifactId,
                    "fixture_property",
                    "fixture_value",
                    "imported",
                    "integration-test",
                    "verified",
                    "integration-test",
                    null,
                    "Integration fixture metadata.",
                    null,
                    null)
            ]);
    }

    private static KnowledgeDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<KnowledgeDbContext>()
                .UseNpgsql(
                    KnowledgeStoreTestConnection.Resolve(),
                    options => options.UseVector())
                .Options;

        return new KnowledgeDbContext(options);
    }
}
