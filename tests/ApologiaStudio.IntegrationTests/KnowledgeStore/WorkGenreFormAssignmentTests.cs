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
/// The authoritative Genre/Form assignments of a Work, after GF-TAX-4.
/// </summary>
/// <remarks>
/// A Work now carries Apologia product terms. These verify the invariants the
/// cutover had to preserve — zero is valid, many are valid, a pair is unique,
/// only an active canonical term persists — and the shape of the relation
/// itself, so a later slice cannot quietly re-point it.
/// </remarks>
[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class WorkGenreFormAssignmentTests
{
    #region Methods Invariants

    [Fact]
    public async Task A_work_may_carry_no_genre_form()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var workId = Guid.NewGuid();

        try
        {
            await SeedWorkAsync(connectionString, workId, "Unclassified work");

            await using var context = new KnowledgeDbContext(options);

            Assert.Empty(
                await new PostgreSqlWorkGenreFormAssignmentStore(context)
                    .GetWorkGenreFormsAsync(workId, CancellationToken.None));
        }
        finally
        {
            await RemoveWorkAsync(connectionString, workId);
        }
    }

    [Fact]
    public async Task A_work_may_carry_one_or_many_product_terms()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var workId = Guid.NewGuid();

        try
        {
            await SeedWorkAsync(connectionString, workId, "Multilabel work");

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlWorkGenreFormAssignmentStore(context);

            Assert.True(
                (await store.AssignAsync(
                    workId,
                    "apologetic_writing",
                    CancellationToken.None)).Assigned);

            var single = await store.GetWorkGenreFormsAsync(
                workId,
                CancellationToken.None);
            Assert.Equal(["apologetic_writing"], single.Select(x => x.Code));

            // Two unrelated product genres coexist. The V1 taxonomy is flat, so
            // no pair of terms can stand in a broader/narrower relation.
            Assert.True(
                (await store.AssignAsync(
                    workId,
                    "essays",
                    CancellationToken.None)).Assigned);
            Assert.True(
                (await store.AssignAsync(
                    workId,
                    "biography",
                    CancellationToken.None)).Assigned);

            var many = await store.GetWorkGenreFormsAsync(
                workId,
                CancellationToken.None);

            // Product display order, not label order.
            Assert.Equal(
                ["biography", "essays", "apologetic_writing"],
                many.Select(x => x.Code));
        }
        finally
        {
            await RemoveWorkAsync(connectionString, workId);
        }
    }

    [Fact]
    public async Task The_same_pair_is_never_persisted_twice()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var workId = Guid.NewGuid();

        try
        {
            await SeedWorkAsync(connectionString, workId, "Duplicate work");

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlWorkGenreFormAssignmentStore(context);

            Assert.True(
                (await store.AssignAsync(
                    workId,
                    "sermon",
                    CancellationToken.None)).Assigned);

            var duplicate = await store.AssignAsync(
                workId,
                "sermon",
                CancellationToken.None);

            Assert.False(duplicate.Assigned);
            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM knowledge_work_genre_forms " +
                    $"WHERE work_id = '{workId:D}';"));

            // The database refuses it too, not only the store.
            await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(
                    connectionString,
                    "INSERT INTO knowledge_work_genre_forms " +
                    "(work_id, product_term_id) VALUES " +
                    $"('{workId:D}', " +
                    $"'{ApologiaGenreFormTaxonomy.StableId("sermon"):D}');"));
        }
        finally
        {
            await RemoveWorkAsync(connectionString, workId);
        }
    }

    [Fact]
    public async Task Only_an_active_canonical_product_term_is_assignable()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var workId = Guid.NewGuid();

        try
        {
            await SeedWorkAsync(connectionString, workId, "Closed vocabulary work");

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlWorkGenreFormAssignmentStore(context);

            // Not a product code at all.
            Assert.False(
                (await store.AssignAsync(
                    workId,
                    "hagiographies",
                    CancellationToken.None)).Assigned);

            // An LCGFT identity is not an Apologia product term.
            Assert.False(
                (await store.AssignAsync(
                    workId,
                    "gf2015026051",
                    CancellationToken.None)).Assigned);

            Assert.False(
                (await store.AssignAsync(
                    Guid.NewGuid(),
                    "sermon",
                    CancellationToken.None)).Assigned);

            Assert.Equal(
                0,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM knowledge_work_genre_forms;"));
        }
        finally
        {
            await RemoveWorkAsync(connectionString, workId);
        }
    }

    [Fact]
    public async Task An_unknown_product_term_is_refused_by_the_database()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var workId = Guid.NewGuid();

        try
        {
            await SeedWorkAsync(connectionString, workId, "Foreign key work");

            await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(
                    connectionString,
                    "INSERT INTO knowledge_work_genre_forms " +
                    "(work_id, product_term_id) VALUES " +
                    $"('{workId:D}', " +
                    "'00000000-0000-0000-0000-0000000000ff');"));

            // An authority term identity is no longer acceptable either.
            var authorityTermId = await AuthorityTermIdAsync(
                connectionString,
                "gf2015026051");

            await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(
                    connectionString,
                    "INSERT INTO knowledge_work_genre_forms " +
                    "(work_id, product_term_id) VALUES " +
                    $"('{workId:D}', '{authorityTermId:D}');"));
        }
        finally
        {
            await RemoveWorkAsync(connectionString, workId);
        }
    }

    [Fact]
    public async Task An_assignment_is_removed_by_its_product_code()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var workId = Guid.NewGuid();

        try
        {
            await SeedWorkAsync(connectionString, workId, "Removable work");

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlWorkGenreFormAssignmentStore(context);

            await store.AssignAsync(workId, "prayer", CancellationToken.None);

            Assert.False(
                await store.RemoveAsync(
                    workId,
                    "sermon",
                    CancellationToken.None));
            Assert.True(
                await store.RemoveAsync(
                    workId,
                    "prayer",
                    CancellationToken.None));
            Assert.Empty(
                await store.GetWorkGenreFormsAsync(
                    workId,
                    CancellationToken.None));
        }
        finally
        {
            await RemoveWorkAsync(connectionString, workId);
        }
    }

    #endregion

    #region Methods Relation Shape

    [Fact]
    public async Task The_relation_targets_the_product_taxonomy_only()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        await PrepareAsync(connectionString);

        Assert.Equal(
            "product_term_id",
            await TextAsync(
                connectionString,
                """
                SELECT column_name FROM information_schema.columns
                WHERE table_name = 'knowledge_work_genre_forms'
                  AND column_name LIKE '%term_id';
                """));

        Assert.Equal(
            1,
            await ScalarAsync(
                connectionString,
                ReferencesQuery(
                    "knowledge_work_genre_forms",
                    "apologia_genre_form_terms")));

        // The old reference is gone, not merely unused.
        Assert.Equal(
            0,
            await ScalarAsync(
                connectionString,
                ReferencesQuery(
                    "knowledge_work_genre_forms",
                    "genre_form_authority_terms")));
    }

    [Fact]
    public async Task The_draft_and_review_relations_are_not_cut_over_yet()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        await PrepareAsync(connectionString);

        // GF-TAX-5 owns these two. Cutting them here would move a reviewer's
        // in-flight selections before anything could read the new vocabulary.
        Assert.Equal(
            1,
            await ScalarAsync(
                connectionString,
                ReferencesQuery(
                    "document_manager_editorial_draft_genre_forms",
                    "genre_form_authority_terms")));
        Assert.Equal(
            1,
            await ScalarAsync(
                connectionString,
                ReferencesQuery(
                    "metadata_review_suggestions",
                    "genre_form_authority_terms")));

        Assert.Equal(
            0,
            await ScalarAsync(
                connectionString,
                ReferencesQuery(
                    "document_manager_editorial_draft_genre_forms",
                    "apologia_genre_form_terms")));
        Assert.Equal(
            0,
            await ScalarAsync(
                connectionString,
                ReferencesQuery(
                    "metadata_review_suggestions",
                    "apologia_genre_form_terms")));
    }

    [Fact]
    public async Task The_authority_catalogue_and_its_mappings_are_untouched()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var workId = Guid.NewGuid();

        var termsBefore = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM genre_form_authority_terms;");
        var mappingsBefore = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM genre_form_authority_mappings;");

        try
        {
            await SeedWorkAsync(connectionString, workId, "Isolation work");

            await using var context = new KnowledgeDbContext(options);

            await new PostgreSqlWorkGenreFormAssignmentStore(context)
                .AssignAsync(workId, "catechism", CancellationToken.None);

            Assert.Equal(
                termsBefore,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM genre_form_authority_terms;"));
            Assert.Equal(
                mappingsBefore,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM genre_form_authority_mappings;"));
        }
        finally
        {
            await RemoveWorkAsync(connectionString, workId);
        }
    }

    #endregion

    #region Methods Helpers

    /// <summary>
    /// Counts the foreign keys of one table that target another.
    /// </summary>
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

    /// <summary>
    /// Migrates and makes sure the canonical taxonomy is present, since the
    /// relation now depends on it.
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
    /// Imports the pinned LCGFT subset, so the test that proves an authority
    /// identity is no longer acceptable has a real one to offer.
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

    private static async Task SeedWorkAsync(
        string connectionString,
        Guid workId,
        string title)
    {
        await ExecuteAsync(
            connectionString,
            $"""
            INSERT INTO knowledge_resources (id, editorial_review_status, created_at)
            VALUES ('{workId:D}', 'approved', now())
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO knowledge_works (id, title)
            VALUES ('{workId:D}', '{title}')
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    private static async Task RemoveWorkAsync(
        string connectionString,
        Guid workId)
    {
        await ExecuteAsync(
            connectionString,
            $"""
            DELETE FROM knowledge_work_genre_forms WHERE work_id = '{workId:D}';
            DELETE FROM knowledge_works WHERE id = '{workId:D}';
            DELETE FROM knowledge_resources WHERE id = '{workId:D}';
            """);
    }

    private static async Task<Guid> AuthorityTermIdAsync(
        string connectionString,
        string authorityIdentifier)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT id FROM genre_form_authority_terms " +
            "WHERE authority_identifier = @identifier;",
            connection);
        command.Parameters.AddWithValue("identifier", authorityIdentifier);

        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> TextAsync(
        string connectionString,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        return (string)(await command.ExecuteScalarAsync())!;
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
