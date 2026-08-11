using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

public sealed class PostgreSqlKnowledgePersistenceTests
{
    private static readonly string[] ExpectedTables =
    [
        "knowledge_artifacts",
        "knowledge_chunk_embeddings",
        "knowledge_chunk_segments",
        "knowledge_contributions",
        "knowledge_contributor_identifiers",
        "knowledge_contributors",
        "knowledge_document_segments",
        "knowledge_evidence_role_assertions",
        "knowledge_evidence_roles",
        "knowledge_expression_relations",
        "knowledge_expressions",
        "knowledge_manifestation_identifiers",
        "knowledge_manifestations",
        "knowledge_metadata_assertions",
        "knowledge_perspective_assertions",
        "knowledge_perspectives",
        "knowledge_processing_activities",
        "knowledge_resources",
        "knowledge_retrieval_chunks",
        "knowledge_source_kind_assertions",
        "knowledge_source_kinds",
        "knowledge_works"
    ];

    [Fact]
    public async Task KnowledgeModel_ShouldMigrateAndExposeAcceptedTables()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingMigrations);

        var mappedTables = context.Model
            .GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(tableName => tableName is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var table in ExpectedTables)
        {
            Assert.Contains(table, mappedTables);
        }

        await using var connection = new NpgsqlConnection(
            KnowledgeStoreTestConnection.Resolve());
        await connection.OpenAsync();

        foreach (var table in ExpectedTables)
        {
            await using var command = new NpgsqlCommand(
                "SELECT to_regclass(@table_name) IS NOT NULL",
                connection);
            command.Parameters.AddWithValue("table_name", $"public.{table}");

            Assert.True(
                Assert.IsType<bool>(await command.ExecuteScalarAsync()),
                $"Expected Knowledge table '{table}' was not created.");
        }
    }

    [Fact]
    public async Task KnowledgeStore_ShouldPersistAuditableDocumentChain()
    {
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(
            KnowledgeStoreTestConnection.Resolve());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var workId = Guid.NewGuid();
        var expressionId = Guid.NewGuid();
        var manifestationId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var segmentId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var embeddingId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var command = new NpgsqlCommand(
                         """
                         INSERT INTO knowledge_resources
                             (id, editorial_review_status, created_at)
                         VALUES
                             (@work_id, 'approved', @now),
                             (@expression_id, 'approved', @now),
                             (@manifestation_id, 'approved', @now),
                             (@artifact_id, 'approved', @now),
                             (@segment_id, 'approved', @now),
                             (@contributor_id, 'approved', @now);

                         INSERT INTO knowledge_works
                             (id, title, original_language, description)
                         VALUES
                             (@work_id, 'De Decretis', 'grc', 'Integration-test work');

                         INSERT INTO knowledge_expressions
                             (id, work_id, language_code, label, description)
                         VALUES
                             (@expression_id, @work_id, 'en', 'English expression', NULL);

                         INSERT INTO knowledge_manifestations
                             (id, expression_id, edition_statement, publication_year, publication_place, citation_label)
                         VALUES
                             (@manifestation_id, @expression_id, 'Test edition', 1892, 'Integration test', 'NPNF II.4');

                         INSERT INTO knowledge_contributors
                             (id, contributor_type, preferred_name, sort_name, description)
                         VALUES
                             (@contributor_id, 'person', 'Athanasius', 'Athanasius', NULL);

                         INSERT INTO knowledge_contributions
                             (contributor_id, work_id, expression_id, manifestation_id, role, attribution_status, ordinal)
                         VALUES
                             (@contributor_id, @work_id, NULL, NULL, 'author', 'established', 0);

                         INSERT INTO knowledge_artifacts
                             (id, manifestation_id, derived_from_artifact_id, artifact_type, sha256,
                              media_type, byte_length, origin_uri, acquired_at, lifecycle_status)
                         VALUES
                             (@artifact_id, @manifestation_id, NULL, 'normalized',
                              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
                              'text/plain', 128, 'https://example.invalid/de-decretis.txt', @now, 'active');

                         INSERT INTO knowledge_document_segments
                             (id, artifact_id, parent_segment_id, segment_type, ordinal, title, text, locator)
                         VALUES
                             (@segment_id, @artifact_id, NULL, 'section', 20, 'Section 20',
                              'Representative integration-test text.', '§20');

                         INSERT INTO knowledge_retrieval_chunks
                             (id, artifact_id, ordinal, text, chunking_strategy, chunking_version, created_at)
                         VALUES
                             (@chunk_id, @artifact_id, 0, 'Representative integration-test text.',
                              'segment-window', 'v1', @now);

                         INSERT INTO knowledge_chunk_segments
                             (chunk_id, segment_id, sequence, start_offset, end_offset)
                         VALUES
                             (@chunk_id, @segment_id, 0, 0, 37);

                         INSERT INTO knowledge_chunk_embeddings
                             (id, chunk_id, embedding_profile, provider, model, model_digest,
                              dimensions, embedding, created_at)
                         VALUES
                             (@embedding_id, @chunk_id, 'integration-test-v1', 'integration-test',
                              'integration-test-model',
                              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
                              3, '[1,0,0]'::vector, @now);
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("work_id", workId);
            command.Parameters.AddWithValue("expression_id", expressionId);
            command.Parameters.AddWithValue("manifestation_id", manifestationId);
            command.Parameters.AddWithValue("artifact_id", artifactId);
            command.Parameters.AddWithValue("segment_id", segmentId);
            command.Parameters.AddWithValue("contributor_id", contributorId);
            command.Parameters.AddWithValue("chunk_id", chunkId);
            command.Parameters.AddWithValue("embedding_id", embeddingId);
            command.Parameters.AddWithValue("now", now);

            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand(
                         """
                         SELECT
                             w.title,
                             e.language_code,
                             m.citation_label,
                             a.sha256,
                             s.locator,
                             c.preferred_name
                         FROM knowledge_document_segments s
                         JOIN knowledge_artifacts a ON a.id = s.artifact_id
                         JOIN knowledge_manifestations m ON m.id = a.manifestation_id
                         JOIN knowledge_expressions e ON e.id = m.expression_id
                         JOIN knowledge_works w ON w.id = e.work_id
                         JOIN knowledge_contributions contribution ON contribution.work_id = w.id
                         JOIN knowledge_contributors c ON c.id = contribution.contributor_id
                         WHERE s.id = @segment_id
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("segment_id", segmentId);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("De Decretis", reader.GetString(0));
            Assert.Equal("en", reader.GetString(1));
            Assert.Equal("NPNF II.4", reader.GetString(2));
            Assert.Equal(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                reader.GetString(3).Trim());
            Assert.Equal("§20", reader.GetString(4));
            Assert.Equal("Athanasius", reader.GetString(5));
            Assert.False(await reader.ReadAsync());
        }

        await using (var command = new NpgsqlCommand(
                         """
                         SELECT dimensions, vector_dims(embedding), model_digest
                         FROM knowledge_chunk_embeddings
                         WHERE id = @embedding_id
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("embedding_id", embeddingId);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(3, reader.GetInt32(0));
            Assert.Equal(3, reader.GetInt32(1));
            Assert.Equal(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                reader.GetString(2).Trim());
            Assert.False(await reader.ReadAsync());
        }

        await transaction.RollbackAsync();
    }

    private static KnowledgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(
                KnowledgeStoreTestConnection.Resolve(),
                options => options.UseVector())
            .Options;

        return new KnowledgeDbContext(options);
    }
}
