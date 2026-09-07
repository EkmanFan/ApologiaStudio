using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

/// <summary>
/// The persisted boundary between the product taxonomy and the external LCGFT
/// catalogue, after the profile removal.
/// </summary>
/// <remarks>
/// One test per claim the cleanup makes: the product path is complete and
/// self-contained, the old profile mechanism is gone from the schema, and the
/// authority catalogue survives intact as an authority.
/// </remarks>
[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class GenreFormSchemaBoundaryTests
{
    #region Methods Product Path

    [Fact]
    public async Task The_product_taxonomy_is_intact()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        await PrepareAsync(connectionString);

        Assert.Equal(
            27,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM apologia_genre_form_terms " +
                "WHERE status = 'active';"));
        Assert.Equal(
            24,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM apologia_genre_form_terms " +
                "WHERE prediction_mode = 'encoder_predictable';"));
        Assert.Equal(
            3,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM apologia_genre_form_terms " +
                "WHERE prediction_mode = 'manual_only';"));
    }

    [Fact]
    public async Task The_twelve_lcgft_alignments_are_intact()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await using (var context = new KnowledgeDbContext(options))
        {
            await EnsureLcgftAsync(context);
        }

        await using (var context = new KnowledgeDbContext(options))
        {
            var applied = await new PostgreSqlGenreFormAuthorityMappingSeeder(
                    context,
                    TimeProvider.System)
                .ApplyAsync(CancellationToken.None);

            Assert.Equal(12, applied.MappingCount);
            Assert.Equal(10, applied.ExactCount);
            Assert.Equal(2, applied.BroaderCount);
        }
    }

    [Fact]
    public async Task Every_product_relation_targets_the_product_taxonomy()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        await PrepareAsync(connectionString);

        foreach (var table in new[]
                 {
                     "knowledge_work_genre_forms",
                     "document_manager_editorial_draft_genre_forms",
                     "metadata_review_suggestions"
                 })
        {
            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    ReferencesQuery(table, "apologia_genre_form_terms")));

            // No product path consults the external catalogue any more.
            Assert.Equal(
                0,
                await ScalarAsync(
                    connectionString,
                    ReferencesQuery(table, "genre_form_authority_terms")));
        }
    }

    #endregion

    #region Methods Removed Mechanism

    [Fact]
    public async Task The_old_profile_table_is_gone()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        await PrepareAsync(connectionString);

        Assert.Equal(
            0,
            await ScalarAsync(
                connectionString,
                """
                SELECT count(*) FROM information_schema.tables
                WHERE table_name = 'genre_form_profile_entries';
                """));
    }

    #endregion

    #region Methods External Authority

    [Fact]
    public async Task The_authority_catalogue_and_its_relations_survive()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await using (var context = new KnowledgeDbContext(options))
        {
            await EnsureLcgftAsync(context);
        }

        // The 22 pinned LCGFT concepts, their variants and their thesaurus
        // relations remain. They describe the authority, not the product.
        Assert.Equal(
            22,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_terms " +
                "WHERE authority = 'lcgft';"));

        Assert.True(
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_broader_relations;") > 0);
        Assert.True(
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_variants;") > 0);
    }

    [Fact]
    public async Task A_reimport_is_still_idempotent_and_reports_nothing_to_review()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await using (var context = new KnowledgeDbContext(options))
        {
            await EnsureLcgftAsync(context);
        }

        await using (var context = new KnowledgeDbContext(options))
        {
            var again = await EnsureLcgftAsync(context);

            Assert.True(again.SnapshotAlreadyImported);

            // Every approved alignment still points at a published concept.
            Assert.Empty(again.AuthorityReviewItems);
        }
    }

    #endregion

    #region Methods Helpers

    private static string ReferencesQuery(
        string dependentTable,
        string principalTable) =>
        $"""
        SELECT count(*) FROM information_schema.referential_constraints rc
        JOIN information_schema.table_constraints tc
          ON tc.constraint_name = rc.unique_constraint_name
        JOIN information_schema.key_column_usage kcu
          ON kcu.constraint_name = rc.constraint_name
        WHERE tc.table_name = '{principalTable}'
          AND kcu.table_name = '{dependentTable}';
        """;

    private static async Task<DbContextOptions<KnowledgeDbContext>> PrepareAsync(
        string connectionString)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, builder => builder.UseVector())
            .Options;

        await using (var context = new KnowledgeDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        await using (var context = new KnowledgeDbContext(options))
        {
            await new PostgreSqlApologiaGenreFormTaxonomySeeder(
                    context,
                    TimeProvider.System)
                .ApplyAsync(CancellationToken.None);
        }

        return options;
    }

    private static async Task<GenreFormAuthorityImportResult> EnsureLcgftAsync(
        KnowledgeDbContext context)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "lcgft-profile-v1-fixture.jsonl");

        var payload = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(payload))
            .ToLowerInvariant();

        using var content = new MemoryStream(payload, writable: false);
        var dataset = new SkosJsonLdGenreFormDatasetReader().Read(content);

        return await new PostgreSqlGenreFormAuthorityStore(context).ImportAsync(
            new GenreFormAuthoritySnapshot(
                "lcgft",
                "https://id.loc.gov/download/authorities/genreForms.skosrdf.jsonld.gz",
                sha256,
                new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero),
                "integration-fixture"),
            dataset,
            CancellationToken.None);
    }

    private static async Task<int> ScalarAsync(
        string connectionString,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    #endregion
}
