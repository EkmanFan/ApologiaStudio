using Npgsql;
using Pgvector;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

public sealed class PostgreSqlKnowledgeStoreInfrastructureTests
{
    [Fact]
    public async Task KnowledgeStore_ShouldEnablePgvectorAndRoundTripNearestVector()
    {
        var connectionString = ResolveConnectionString();
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();

        await using var dataSource = dataSourceBuilder.Build();
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var extensionCommand = new NpgsqlCommand(
                         """
                         SELECT extversion
                         FROM pg_extension
                         WHERE extname = 'vector'
                         """,
                         connection))
        {
            var extensionVersion = Assert.IsType<string>(
                await extensionCommand.ExecuteScalarAsync());

            Assert.False(string.IsNullOrWhiteSpace(extensionVersion));
        }

        await using (var createCommand = new NpgsqlCommand(
                         """
                         CREATE TEMP TABLE vector_probe
                         (
                             label text NOT NULL,
                             embedding vector(3) NOT NULL
                         )
                         """,
                         connection))
        {
            await createCommand.ExecuteNonQueryAsync();
        }

        await using (var insertCommand = new NpgsqlCommand(
                         """
                         INSERT INTO vector_probe (label, embedding)
                         VALUES
                             (@near_label, @near_embedding),
                             (@far_label, @far_embedding)
                         """,
                         connection))
        {
            insertCommand.Parameters.AddWithValue("near_label", "near");
            insertCommand.Parameters.AddWithValue(
                "near_embedding",
                new Vector(new float[] { 1f, 0f, 0f }));
            insertCommand.Parameters.AddWithValue("far_label", "far");
            insertCommand.Parameters.AddWithValue(
                "far_embedding",
                new Vector(new float[] { 0f, 1f, 0f }));

            await insertCommand.ExecuteNonQueryAsync();
        }

        await using var nearestCommand = new NpgsqlCommand(
            """
            SELECT label
            FROM vector_probe
            ORDER BY embedding <-> @query
            LIMIT 1
            """,
            connection);

        nearestCommand.Parameters.AddWithValue(
            "query",
            new Vector(new float[] { 0.9f, 0.1f, 0f }));

        var nearest = Assert.IsType<string>(
            await nearestCommand.ExecuteScalarAsync());

        Assert.Equal("near", nearest);
    }

    private static string ResolveConnectionString()
    {
        var explicitConnectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_KNOWLEDGE_TEST_DB_CONNECTION");

        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var password = Environment.GetEnvironmentVariable(
            "APOLOGIA_KNOWLEDGE_DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Neither APOLOGIASTUDIO_KNOWLEDGE_TEST_DB_CONNECTION nor " +
                "APOLOGIA_KNOWLEDGE_DB_PASSWORD was configured.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 54330,
            Database = "apologia_knowledge_test",
            Username = "apologia_knowledge",
            Password = password,
            Pooling = false
        }.ConnectionString;
    }
}
