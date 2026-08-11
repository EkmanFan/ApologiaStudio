using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

public sealed class PostgreSqlKnowledgeVectorIndexTests
{
    private const string IndexName =
        "ix_knowledge_chunk_embeddings_qwen3_4b_hnsw_cosine";

    [Fact]
    public async Task KnowledgeMigration_ShouldCreatePinnedPartialHalfVectorHnswIndex()
    {
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            Assert.Empty(pendingMigrations);
        }

        await using var connection = new NpgsqlConnection(
            KnowledgeStoreTestConnection.Resolve());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'knowledge_chunk_embeddings'
              AND indexname = @index_name
            """,
            connection);
        command.Parameters.AddWithValue("index_name", IndexName);

        var definition = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.Contains("USING hnsw", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("halfvec(2560)", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("halfvec_cosine_ops", definition, StringComparison.OrdinalIgnoreCase);
        // pg_indexes normalizes partial-index predicates and may add explicit type casts.
        Assert.Contains("embedding_profile", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'de-decretis-retrieval-qwen3-embedding-4b-v1'", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dimensions", definition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2560", definition, StringComparison.OrdinalIgnoreCase);
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
