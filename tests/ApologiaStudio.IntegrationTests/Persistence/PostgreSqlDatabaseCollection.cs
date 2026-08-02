namespace ApologiaStudio.IntegrationTests.Persistence;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlDatabaseCollection
{
    public const string Name = "PostgreSQL database";
}
