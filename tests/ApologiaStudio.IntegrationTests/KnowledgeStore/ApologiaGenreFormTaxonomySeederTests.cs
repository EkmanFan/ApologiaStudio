using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

/// <summary>
/// Seeding of the canonical V1 product taxonomy.
/// </summary>
/// <remarks>
/// GF-TAX-2 introduces the taxonomy and nothing consumes it yet, so these tests
/// verify the two properties that matter before anything does: the applied
/// shape is exactly what the product declares, and a re-run reaches the same
/// rows instead of minting new ones.
///
/// They also assert what the slice must NOT have done — no existing foreign key
/// re-pointed, and the external LCGFT catalogue untouched.
/// </remarks>
[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class ApologiaGenreFormTaxonomySeederTests
{
    #region Methods

    [Fact]
    public async Task Seeding_applies_exactly_the_declared_taxonomy()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        var applied = await SeedAsync(options);

        Assert.Equal("apologia-genre-form-v1", applied.TaxonomyVersion);
        Assert.Equal(27, applied.ActiveTermCount);
        Assert.Equal(24, applied.EncoderPredictableCount);
        Assert.Equal(3, applied.ManualOnlyCount);

        Assert.Equal(
            27,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM apologia_genre_form_terms " +
                "WHERE taxonomy_version = 'apologia-genre-form-v1' " +
                "AND status = 'active';"));
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
    public async Task Seeding_twice_changes_nothing_and_keeps_the_same_identities()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        var identitiesAfterFirst = await IdentitiesAsync(connectionString);

        var second = await SeedAsync(options);

        Assert.False(second.Changed);
        Assert.Equal(0, second.InsertedCount);
        Assert.Equal(0, second.UpdatedCount);
        Assert.Equal(27, second.ActiveTermCount);

        // Identity is derived from the code, so a re-run reaches the same rows.
        Assert.Equal(
            identitiesAfterFirst,
            await IdentitiesAsync(connectionString));
    }

    [Fact]
    public async Task Applied_identities_match_the_canonical_derivation()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        var rows = await IdentitiesAsync(connectionString);

        foreach (var term in ApologiaGenreFormTaxonomy.Terms)
        {
            Assert.Equal(term.Id, rows[term.Code]);
        }
    }

    [Fact]
    public async Task An_incompatible_identity_fails_closed_rather_than_being_rewritten()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        // Simulates a row that carries a canonical code under a foreign
        // identity. Rewriting it would silently re-point every assignment that
        // already referenced it.
        await ExecuteAsync(
            connectionString,
            "UPDATE apologia_genre_form_terms " +
            "SET id = '00000000-0000-0000-0000-0000000000ff' " +
            "WHERE code = 'creed';");

        await using var context = new KnowledgeDbContext(options);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new PostgreSqlApologiaGenreFormTaxonomySeeder(
                    context,
                    TimeProvider.System)
                .ApplyAsync(CancellationToken.None));

        Assert.Contains("creed", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "Refusing to rewrite",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_slice_repoints_no_existing_foreign_key()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        // The three canonical references still target the authority catalogue.
        // Re-pointing them is a later slice, and doing it here would move
        // assignments before anything could read the new taxonomy.
        Assert.Equal(
            3,
            await ScalarAsync(
                connectionString,
                """
                SELECT count(*) FROM information_schema.referential_constraints rc
                JOIN information_schema.table_constraints tc
                  ON tc.constraint_name = rc.unique_constraint_name
                JOIN information_schema.key_column_usage kcu
                  ON kcu.constraint_name = rc.constraint_name
                WHERE tc.table_name = 'genre_form_authority_terms'
                  AND kcu.table_name IN (
                    'knowledge_work_genre_forms',
                    'document_manager_editorial_draft_genre_forms',
                    'metadata_review_suggestions');
                """));
    }

    [Fact]
    public async Task The_external_authority_catalogue_is_untouched()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        var before = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM genre_form_authority_terms;");

        await SeedAsync(options);

        Assert.Equal(
            before,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_terms;"));
    }

    #endregion

    #region Methods Helpers

    private static async Task<ApologiaGenreFormTaxonomySeedResult> SeedAsync(
        DbContextOptions<KnowledgeDbContext> options)
    {
        await using var context = new KnowledgeDbContext(options);

        return await new PostgreSqlApologiaGenreFormTaxonomySeeder(
                context,
                TimeProvider.System)
            .ApplyAsync(CancellationToken.None);
    }

    private static async Task<Dictionary<string, Guid>> IdentitiesAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT code, id FROM apologia_genre_form_terms;",
            connection);

        var identities = new Dictionary<string, Guid>(StringComparer.Ordinal);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            identities[reader.GetString(0)] = reader.GetGuid(1);
        }

        return identities;
    }

    private static async Task<DbContextOptions<KnowledgeDbContext>> PrepareAsync(
        string connectionString)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, builder => builder.UseVector())
            .Options;

        await using var context = new KnowledgeDbContext(options);
        await context.Database.MigrateAsync();

        // Alignments reference the taxonomy, so they go first.
        await ExecuteAsync(
            connectionString,
            "DELETE FROM genre_form_authority_mappings;");
        await ExecuteAsync(
            connectionString,
            "DELETE FROM apologia_genre_form_terms;");

        return options;
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

    private static async Task ExecuteAsync(
        string connectionString,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    #endregion
}
