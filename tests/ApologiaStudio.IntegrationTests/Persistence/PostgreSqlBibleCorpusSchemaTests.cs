using ApologiaStudio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlBibleCorpusSchemaTests
{
    private static readonly string[] CanonicalTableNames =
    [
        "bible_editions",
        "bible_corpus_versions",
        "bible_source_artifacts",
        "bible_books",
        "bible_corpus_books",
        "bible_verses",
        "bible_word_annotations",
        "bible_supplemental_texts"
    ];

    [Fact]
    public async Task Migration_ShouldCreateCanonicalSchemaAndSeedProtestantBookCatalog()
    {
        await using var context = CreateContext();

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = ANY (@table_names)
            """;

        var tableNamesParameter = command.CreateParameter();
        tableNamesParameter.ParameterName = "table_names";
        tableNamesParameter.Value = CanonicalTableNames;
        command.Parameters.Add(tableNamesParameter);

        var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(CanonicalTableNames.Length, tableCount);

        command.Parameters.Clear();
        command.CommandText = "SELECT COUNT(*) FROM bible_books";
        var bookCount = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.Equal(66, bookCount);

        command.CommandText =
            """
            SELECT string_agg(usfm_code || ':' || osis_code, ',' ORDER BY canonical_order)
            FROM bible_books
            WHERE canonical_order IN (1, 66)
            """;
        var boundaryBooks = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.Equal("GEN:Gen,REV:Rev", boundaryBooks);
    }

    [Fact]
    public async Task Database_ShouldAllowOnlyOneActiveVersionPerEdition()
    {
        await using var context = CreateContext();

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO bible_editions (code, display_name, language_tag, canon_code)
            VALUES ('web-classic', 'World English Bible Classic', 'en', 'protestant-66')
            """);

        var importedAt = new DateTimeOffset(2026, 8, 2, 16, 0, 0, TimeSpan.Zero);

        await InsertApprovedActiveVersionAsync(
            context,
            Guid.NewGuid(),
            new string('a', 64),
            new string('b', 64),
            importedAt);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertApprovedActiveVersionAsync(
                context,
                Guid.NewGuid(),
                new string('c', 64),
                new string('d', 64),
                importedAt.AddMinutes(1)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("ux_bible_corpus_versions_active_edition", exception.ConstraintName);
    }

    private static ApologiaStudioDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_TEST_DB_CONNECTION");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        var options = new DbContextOptionsBuilder<ApologiaStudioDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApologiaStudioDbContext(options);
    }

    private static Task<int> InsertApprovedActiveVersionAsync(
        ApologiaStudioDbContext context,
        Guid id,
        string sourceTreeSha256,
        string importFingerprint,
        DateTimeOffset importedAt) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO bible_corpus_versions
                (id, edition_code, source_tree_sha256, import_fingerprint,
                 parser_name, parser_version, normalization_policy_id,
                 canonical_schema_version, imported_at, approved_at,
                 validation_status, is_active)
            VALUES
                ({id}, 'web-classic', {sourceTreeSha256}, {importFingerprint},
                 'SIL.Machine', '3.9.1', 'unicode-nfc-collapse-whitespace-v1',
                 1, {importedAt}, {importedAt}, 'approved', TRUE)
            """);
}
