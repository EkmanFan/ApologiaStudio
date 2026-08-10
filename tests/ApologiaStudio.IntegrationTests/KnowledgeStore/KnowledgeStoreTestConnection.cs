using Npgsql;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

internal static class KnowledgeStoreTestConnection
{
    public static string Resolve()
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
