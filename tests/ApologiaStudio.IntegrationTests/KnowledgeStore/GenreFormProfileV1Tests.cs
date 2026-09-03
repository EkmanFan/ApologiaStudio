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
/// Acceptance coverage for Apologia Genre/Form Profile V1. These tests assume
/// the official LCGFT authority has been imported into the test database.
/// </summary>
[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class GenreFormProfileV1Tests
{
    [Fact]
    public async Task Profile_selects_exactly_the_approved_terms()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await EnsureAuthorityImportedAsync(options);

        await using var context = new KnowledgeDbContext(options);
        var seeder = new PostgreSqlGenreFormProfileSeeder(context);

        var applied = await seeder.ApplyAsync(CancellationToken.None);

        // AC-GF-01 and the approved set size.
        Assert.Equal(14, applied.SelectableCount);

        // Section 3: ancestors are derived, not declared, and the closure is
        // transitive. Commentaries adds none: its only broader term is
        // Discursive works, already reached through Sermons.
        Assert.Equal(
            [
                "Business correspondence",
                "Correspondence",
                "Creative nonfiction",
                "Discursive works",
                "Informational works",
                "Instructional and educational works",
                "Records (Documents)",
                "Religious materials"
            ],
            applied.StructuralOnlyLabels);
        Assert.Equal(8, applied.StructuralOnlyCount);

        var store = new PostgreSqlGenreFormAuthorityStore(context);
        var selectable = await store.GetSelectableTermsAsync(CancellationToken.None);

        Assert.Equal(14, selectable.Count);
        Assert.Equal(
            GenreFormProfile.SelectableLabels.Order(StringComparer.Ordinal),
            selectable.Select(x => x.PreferredLabel).Order(StringComparer.Ordinal));

        // AC-GF-03: every entry is backed by an imported authority identity.
        Assert.All(
            selectable,
            x =>
            {
                Assert.StartsWith(
                    "http://id.loc.gov/authorities/genreForms/",
                    x.AuthorityUri,
                    StringComparison.Ordinal);
                Assert.NotEmpty(x.AuthorityIdentifier);
            });

        // AC-GF-04: structural ancestors never appear as selectable.
        Assert.DoesNotContain(selectable, x => x.PreferredLabel == "Religious materials");
    }

    [Fact]
    public async Task Profile_seed_is_idempotent()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await EnsureAuthorityImportedAsync(options);

        await using (var context = new KnowledgeDbContext(options))
        {
            await new PostgreSqlGenreFormProfileSeeder(context)
                .ApplyAsync(CancellationToken.None);
        }

        await using (var context = new KnowledgeDbContext(options))
        {
            var again = await new PostgreSqlGenreFormProfileSeeder(context)
                .ApplyAsync(CancellationToken.None);

            Assert.False(again.Changed);
            Assert.Equal(14, again.SelectableCount);
        }
    }

    [Fact]
    public async Task Unresolved_approved_term_fails_closed()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await using var context = new KnowledgeDbContext(options);

        // An approved term the authority does not publish must abort the seed
        // rather than be invented locally.
        var seeder = new PostgreSqlGenreFormProfileSeeder(context);

        await ExecuteAsync(
            connectionString,
            "UPDATE genre_form_authority_terms SET preferred_label = @renamed " +
            "WHERE preferred_label = @original",
            ("renamed", "Apologetic writings (withdrawn)"),
            ("original", "Apologetic writings"));

        try
        {
            var exception = await Assert.ThrowsAsync<GenreFormAuthorityException>(
                () => seeder.ApplyAsync(CancellationToken.None));

            Assert.Contains("Apologetic writings", exception.Message);
        }
        finally
        {
            await ExecuteAsync(
                connectionString,
                "UPDATE genre_form_authority_terms SET preferred_label = @original " +
                "WHERE preferred_label = @renamed",
                ("renamed", "Apologetic writings (withdrawn)"),
                ("original", "Apologetic writings"));
        }
    }

    [Fact]
    public async Task Assignments_are_explicit_bounded_and_never_inferred()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await EnsureAuthorityImportedAsync(options);

        await using (var context = new KnowledgeDbContext(options))
        {
            await new PostgreSqlGenreFormProfileSeeder(context)
                .ApplyAsync(CancellationToken.None);
        }

        var workId = Guid.NewGuid();
        await SeedWorkAsync(connectionString, workId, "Genre/Form acceptance work");

        try
        {
            var apologetic = await UriForAsync(connectionString, "Apologetic writings");
            var essays = await UriForAsync(connectionString, "Essays");
            var hagiographies = await UriForAsync(connectionString, "Hagiographies");
            var biographies = await UriForAsync(connectionString, "Biographies");
            var religiousMaterials = await UriForAsync(connectionString, "Religious materials");

            await using var context = new KnowledgeDbContext(options);
            var authority = new PostgreSqlGenreFormAuthorityStore(context);
            var assignments = new PostgreSqlGenreFormAssignmentStore(context, authority);

            // AC-GF-06: zero assignments is valid.
            Assert.Empty(
                await assignments.GetWorkGenreFormsAsync(workId, CancellationToken.None));

            // AC-GF-11 / GF-RULE-13: two independent genres may coexist.
            Assert.True(
                (await assignments.AssignAsync(workId, apologetic, CancellationToken.None))
                .Assigned);
            Assert.True(
                (await assignments.AssignAsync(workId, essays, CancellationToken.None))
                .Assigned);

            var assigned = await assignments.GetWorkGenreFormsAsync(
                workId,
                CancellationToken.None);
            Assert.Equal(2, assigned.Count);

            // AC-GF-08: the same pair cannot be persisted twice.
            var duplicate = await assignments.AssignAsync(
                workId,
                apologetic,
                CancellationToken.None);
            Assert.False(duplicate.Assigned);

            // AC-GF-04 / GF-RULE-12: a structural term is not assignable.
            var structural = await assignments.AssignAsync(
                workId,
                religiousMaterials,
                CancellationToken.None);
            Assert.False(structural.Assigned);

            // AC-GF-05 and AC-GF-10: assigning a narrower term persists only
            // that term, and its ancestor may not be added afterwards.
            Assert.True(
                (await assignments.AssignAsync(workId, hagiographies, CancellationToken.None))
                .Assigned);

            var ancestor = await assignments.AssignAsync(
                workId,
                biographies,
                CancellationToken.None);
            Assert.False(ancestor.Assigned);

            var finalState = await assignments.GetWorkGenreFormsAsync(
                workId,
                CancellationToken.None);

            Assert.Equal(3, finalState.Count);
            Assert.DoesNotContain(finalState, x => x.PreferredLabel == "Biographies");
        }
        finally
        {
            await ExecuteAsync(
                connectionString,
                "DELETE FROM knowledge_work_genre_forms WHERE work_id = @id;" +
                "DELETE FROM knowledge_works WHERE id = @id;" +
                "DELETE FROM knowledge_resources WHERE id = @id;",
                ("id", workId));
        }
    }

    [Fact]
    public async Task Recognized_variants_resolve_to_the_authorized_term()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await EnsureAuthorityImportedAsync(options);

        // AC-GF-09 / GF-RULE-07: a variant is never a second Genre/Form value.
        Assert.Equal(
            "Sermons",
            await LabelForVariantAsync(connectionString, "Homilies"));
        Assert.Equal(
            "Creeds",
            await LabelForVariantAsync(connectionString, "Confessions of faith"));
    }

    [Fact]
    public async Task Applying_the_profile_creates_no_assignment_and_leaves_source_kinds_alone()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await EnsureAuthorityImportedAsync(options);

        var before = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM knowledge_work_genre_forms");
        var sourceKindsBefore = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM knowledge_source_kinds");

        await using (var context = new KnowledgeDbContext(options))
        {
            await new PostgreSqlGenreFormProfileSeeder(context)
                .ApplyAsync(CancellationToken.None);
        }

        // AC-GF-11 and AC-GF-13.
        Assert.Equal(
            before,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM knowledge_work_genre_forms"));
        Assert.Equal(
            sourceKindsBefore,
            await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM knowledge_source_kinds"));
    }

    /// <summary>
    /// Imports a pinned subset of the official LCGFT dataset: the thirteen
    /// approved terms plus the transitive ancestors they actually declare.
    /// </summary>
    private static async Task EnsureAuthorityImportedAsync(
        DbContextOptions<KnowledgeDbContext> options)
    {
        await using var context = new KnowledgeDbContext(options);

        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "lcgft-profile-v1-fixture.jsonl");

        // Snapshot identity is the fixture's own content hash, exactly as in
        // production: editing the fixture yields a new snapshot and a real
        // re-import instead of an idempotent no-op over stale data.
        var payload = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

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

    private static async Task<string> UriForAsync(
        string connectionString,
        string preferredLabel)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT authority_uri FROM genre_form_authority_terms " +
            "WHERE preferred_label = @label",
            connection);
        command.Parameters.AddWithValue("label", preferredLabel);

        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> LabelForVariantAsync(
        string connectionString,
        string variant)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT t.preferred_label
            FROM genre_form_authority_variants v
            JOIN genre_form_authority_terms t ON t.id = v.term_id
            WHERE lower(v.label) = lower(@variant)
            """,
            connection);
        command.Parameters.AddWithValue("variant", variant);

        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SeedWorkAsync(
        string connectionString,
        Guid workId,
        string title)
    {
        await ExecuteAsync(
            connectionString,
            """
            INSERT INTO knowledge_resources (id, editorial_review_status, created_at)
            VALUES (@id, 'approved', now())
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO knowledge_works (id, title)
            VALUES (@id, @title)
            ON CONFLICT (id) DO NOTHING;
            """,
            ("id", workId),
            ("title", title));
    }

    private static async Task<DbContextOptions<KnowledgeDbContext>> PrepareAsync(
        string connectionString)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, builder => builder.UseVector())
            .Options;

        await using var context = new KnowledgeDbContext(options);
        await context.Database.MigrateAsync();

        return options;
    }

    private static async Task<int> ScalarAsync(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
