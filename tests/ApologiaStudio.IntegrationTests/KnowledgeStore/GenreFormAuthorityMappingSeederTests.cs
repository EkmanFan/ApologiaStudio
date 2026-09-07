using System.Security.Cryptography;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

/// <summary>
/// Seeding of the approved LCGFT alignments.
/// </summary>
/// <remarks>
/// GF-TAX-3 records alignment and nothing more. These verify the applied set,
/// the fail-closed guards, the database-level constraints, and what the slice
/// must NOT have done — no business foreign key re-pointed, no authority
/// catalogue touched.
/// </remarks>
[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class GenreFormAuthorityMappingSeederTests
{
    #region Methods Applied Shape

    [Fact]
    public async Task Seeding_applies_exactly_the_approved_alignments()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        var applied = await SeedAsync(options);

        Assert.Equal("lcgft", applied.Authority);
        Assert.Equal(12, applied.MappingCount);
        Assert.Equal(10, applied.ExactCount);
        Assert.Equal(2, applied.BroaderCount);
        Assert.Equal(0, applied.NarrowerCount);
        Assert.Equal(12, applied.InsertedCount);
        Assert.True(applied.Changed);

        Assert.Equal(
            12,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_mappings " +
                "WHERE authority = 'lcgft';"));
        Assert.Equal(
            10,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_mappings " +
                "WHERE mapping_kind = 'exact';"));
        Assert.Equal(
            2,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_mappings " +
                "WHERE mapping_kind = 'broader';"));
    }

    [Fact]
    public async Task The_two_broader_alignments_are_the_approved_ones()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        // Read product-term-first, which is the direction the kind is stated
        // in: the Apologia concept is wider than the LCGFT one.
        Assert.Equal(
            "broader|gf2015026031",
            await TextAsync(connectionString, "creed"));
        Assert.Equal(
            "broader|gf2014026039",
            await TextAsync(connectionString, "academic_degree_work"));

        Assert.Equal(
            "exact|gf2015026027",
            await TextAsync(connectionString, "apologetic_writing"));
    }

    [Fact]
    public async Task No_alignment_is_recorded_for_the_excluded_concepts()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        // Pastoral letters and charges, and Hagiographies.
        Assert.Equal(
            0,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_mappings " +
                "WHERE external_concept_id IN " +
                "('gf2015026047', 'gf2015026032');"));
    }

    [Fact]
    public async Task Fifteen_product_terms_stay_unaligned()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        // Absence of an alignment is a valid state, not a gap to be filled.
        Assert.Equal(
            15,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM apologia_genre_form_terms t " +
                "WHERE NOT EXISTS (SELECT 1 " +
                "FROM genre_form_authority_mappings m " +
                "WHERE m.product_term_id = t.id);"));
    }

    [Fact]
    public async Task Every_alignment_points_at_a_canonical_product_term()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        Assert.Equal(
            12,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_mappings m " +
                "JOIN apologia_genre_form_terms t ON t.id = m.product_term_id;"));

        foreach (var mapping in ApologiaGenreFormAuthorityMappings.Approved)
        {
            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM genre_form_authority_mappings m " +
                    "JOIN apologia_genre_form_terms t " +
                    "  ON t.id = m.product_term_id " +
                    $"WHERE t.code = '{mapping.ProductTermCode}' " +
                    $"AND m.external_concept_id = '{mapping.ExternalConceptId}' " +
                    $"AND m.id = '{mapping.Id:D}';"));
        }
    }

    #endregion

    #region Methods Idempotence

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        var stamps = await StampsAsync(connectionString);

        var second = await SeedAsync(options);

        Assert.False(second.Changed);
        Assert.Equal(0, second.InsertedCount);
        Assert.Equal(12, second.MappingCount);

        // Identity is derived from the alignment, so a re-run reaches the same
        // rows and leaves even their timestamps alone.
        Assert.Equal(stamps, await StampsAsync(connectionString));
    }

    #endregion

    #region Methods Fail Closed

    [Fact]
    public async Task A_recorded_kind_that_disagrees_fails_closed()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        await ExecuteAsync(
            connectionString,
            "UPDATE genre_form_authority_mappings SET mapping_kind = 'exact' " +
            "WHERE external_concept_id = 'gf2015026031';");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedAsync(options));

        Assert.Contains("creed", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "Refusing to rewrite",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_alignment_to_another_concept_fails_closed()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        // The same product term aligned to a different LCGFT concept: adding
        // the approved one too would leave two contradictory alignments.
        await ExecuteAsync(
            connectionString,
            "UPDATE genre_form_authority_mappings " +
            "SET external_concept_id = 'gf2015026047' " +
            "WHERE external_concept_id = 'gf2015026031';");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedAsync(options));

        Assert.Contains(
            "already aligned",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_absent_authority_concept_fails_closed()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        // Restored whatever happens: the catalogue is shared by the suite.
        try
        {
            await ExecuteAsync(
                connectionString,
                "UPDATE genre_form_authority_terms " +
                "SET authority_identifier = 'gf-withdrawn' " +
                "WHERE authority_identifier = 'gf2025026014';");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SeedAsync(options));

            Assert.Contains(
                "gf2025026014",
                exception.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "absent from the imported authority catalogue",
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            await ExecuteAsync(
                connectionString,
                "UPDATE genre_form_authority_terms " +
                "SET authority_identifier = 'gf2025026014' " +
                "WHERE authority_identifier = 'gf-withdrawn';");
        }

        // Nothing was written before the guard ran.
        Assert.Equal(
            0,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_mappings;"));
    }

    [Fact]
    public async Task A_renamed_authority_concept_fails_closed()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        try
        {
            await ExecuteAsync(
                connectionString,
                "UPDATE genre_form_authority_terms " +
                "SET preferred_label = 'Confessions of faith' " +
                "WHERE authority_identifier = 'gf2015026031';");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SeedAsync(options));

            Assert.Contains(
                "Confessions of faith",
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            await ExecuteAsync(
                connectionString,
                "UPDATE genre_form_authority_terms " +
                "SET preferred_label = 'Creeds' " +
                "WHERE authority_identifier = 'gf2015026031';");
        }
    }

    #endregion

    #region Methods Constraints

    [Fact]
    public async Task The_database_refuses_an_unknown_authority_or_kind()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        var productTermId = ApologiaGenreFormTaxonomy.StableId("sermon");

        await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(
                connectionString,
                "INSERT INTO genre_form_authority_mappings " +
                "(id, product_term_id, authority, external_concept_id, " +
                " external_concept_uri, mapping_kind, updated_at) VALUES " +
                $"(gen_random_uuid(), '{productTermId:D}', 'viaf', 'x', " +
                "NULL, 'exact', now());"));

        await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(
                connectionString,
                "INSERT INTO genre_form_authority_mappings " +
                "(id, product_term_id, authority, external_concept_id, " +
                " external_concept_uri, mapping_kind, updated_at) VALUES " +
                $"(gen_random_uuid(), '{productTermId:D}', 'lcgft', 'x', " +
                "NULL, 'equivalent', now());"));
    }

    [Fact]
    public async Task The_database_refuses_a_duplicate_alignment_or_an_unknown_term()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        var productTermId = ApologiaGenreFormTaxonomy.StableId("sermon");

        await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(
                connectionString,
                "INSERT INTO genre_form_authority_mappings " +
                "(id, product_term_id, authority, external_concept_id, " +
                " external_concept_uri, mapping_kind, updated_at) VALUES " +
                $"(gen_random_uuid(), '{productTermId:D}', 'lcgft', " +
                "'gf2015026051', NULL, 'exact', now());"));

        await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(
                connectionString,
                "INSERT INTO genre_form_authority_mappings " +
                "(id, product_term_id, authority, external_concept_id, " +
                " external_concept_uri, mapping_kind, updated_at) VALUES " +
                "(gen_random_uuid(), " +
                "'00000000-0000-0000-0000-0000000000ff', 'lcgft', " +
                "'gf2015026051', NULL, 'exact', now());"));
    }

    #endregion

    #region Methods Non Regression

    [Fact]
    public async Task The_authority_catalogue_and_the_taxonomy_are_untouched()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        var termsBefore = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM genre_form_authority_terms;");
        var broaderBefore = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM genre_form_broader_relations;");
        var productBefore = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM apologia_genre_form_terms;");

        await SeedAsync(options);

        Assert.Equal(
            termsBefore,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_authority_terms;"));
        Assert.Equal(
            broaderBefore,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM genre_form_broader_relations;"));
        Assert.Equal(
            productBefore,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM apologia_genre_form_terms;"));
    }

    [Fact]
    public async Task The_slice_repoints_no_existing_foreign_key()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await SeedAsync(options);

        // Since GF-TAX-4 a Work carries a product term. The two remaining
        // references still target the authority catalogue until GF-TAX-5.
        Assert.Equal(
            2,
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
                    'document_manager_editorial_draft_genre_forms',
                    'metadata_review_suggestions');
                """));
    }

    #endregion

    #region Methods Helpers

    private static async Task<GenreFormAuthorityMappingSeedResult> SeedAsync(
        DbContextOptions<KnowledgeDbContext> options)
    {
        await using var context = new KnowledgeDbContext(options);

        return await new PostgreSqlGenreFormAuthorityMappingSeeder(
                context,
                TimeProvider.System)
            .ApplyAsync(CancellationToken.None);
    }

    /// <summary>
    /// Migrates, clears the alignments, applies the canonical taxonomy, and
    /// makes sure the LCGFT subset the alignments target is imported.
    /// </summary>
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

        await ExecuteAsync(
            connectionString,
            "DELETE FROM genre_form_authority_mappings;");

        await using (var context = new KnowledgeDbContext(options))
        {
            await new PostgreSqlApologiaGenreFormTaxonomySeeder(
                    context,
                    TimeProvider.System)
                .ApplyAsync(CancellationToken.None);
        }

        await EnsureAuthorityImportedAsync(options);

        return options;
    }

    /// <summary>
    /// Imports the pinned subset of the official LCGFT dataset, exactly as the
    /// profile tests do, so the alignments are validated against real authority
    /// rows rather than ambient state.
    /// </summary>
    private static async Task EnsureAuthorityImportedAsync(
        DbContextOptions<KnowledgeDbContext> options)
    {
        await using var context = new KnowledgeDbContext(options);

        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "lcgft-profile-v1-fixture.jsonl");

        var payload = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexString(SHA256.HashData(payload))
            .ToLowerInvariant();

        using var content = new MemoryStream(payload, writable: false);
        var dataset = new SkosJsonLdGenreFormDatasetReader().Read(content);

        await new PostgreSqlGenreFormAuthorityStore(context).ImportAsync(
            new GenreFormAuthoritySnapshot(
                "lcgft",
                "https://id.loc.gov/download/authorities/genreForms.skosrdf.jsonld.gz",
                sha256,
                new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero),
                "integration-fixture"),
            dataset,
            CancellationToken.None);
    }

    private static async Task<string> TextAsync(
        string connectionString,
        string productTermCode)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT m.mapping_kind || '|' || m.external_concept_id " +
            "FROM genre_form_authority_mappings m " +
            "JOIN apologia_genre_form_terms t ON t.id = m.product_term_id " +
            "WHERE t.code = @code;",
            connection);
        command.Parameters.AddWithValue("code", productTermCode);

        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Dictionary<Guid, DateTimeOffset>> StampsAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT id, updated_at FROM genre_form_authority_mappings;",
            connection);

        var stamps = new Dictionary<Guid, DateTimeOffset>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            stamps[reader.GetGuid(0)] = reader.GetFieldValue<DateTimeOffset>(1);
        }

        return stamps;
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
