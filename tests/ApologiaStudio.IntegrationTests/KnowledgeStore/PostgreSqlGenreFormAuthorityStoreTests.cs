using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlGenreFormAuthorityStoreTests
{
    private const string Base = "http://id.loc.gov/authorities/genreForms/";

    [Fact]
    public async Task Authority_import_is_idempotent_and_preserves_the_Apologia_profile()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        var prefix = $"gf-{Guid.NewGuid():N}-";
        var sermons = Base + prefix + "sermons";
        var religious = Base + prefix + "religious";
        var instructional = Base + prefix + "instructional";
        var creeds = Base + prefix + "creeds";

        var dataset = new GenreFormAuthorityDataset(
        [
            Term(religious, "Religious works"),
            Term(instructional, "Instructional works"),
            // Two broader terms: the thesaurus is not a tree.
            Term(sermons, "Sermons", broader: [religious, instructional], related: [creeds]),
            Term(creeds, "Creeds", broader: [religious], related: [sermons])
        ]);

        try
        {
            await using (var context = new KnowledgeDbContext(options))
            {
                var store = new PostgreSqlGenreFormAuthorityStore(context);

                var first = await store.ImportAsync(
                    Snapshot("sha-one-" + prefix),
                    dataset,
                    CancellationToken.None);

                Assert.False(first.SnapshotAlreadyImported);
                Assert.Equal(4, first.TermCount);
                Assert.Empty(first.ProfileReviewItems);
            }

            // AC-14: nothing becomes selectable merely by being imported.
            await using (var context = new KnowledgeDbContext(options))
            {
                var store = new PostgreSqlGenreFormAuthorityStore(context);
                var selectable = await store.GetSelectableTermsAsync(CancellationToken.None);

                Assert.DoesNotContain(
                    selectable,
                    x => x.AuthorityUri.StartsWith(Base + prefix, StringComparison.Ordinal));
            }

            // AC-04: identical content imported twice changes nothing.
            await using (var context = new KnowledgeDbContext(options))
            {
                var store = new PostgreSqlGenreFormAuthorityStore(context);

                var again = await store.ImportAsync(
                    Snapshot("sha-one-" + prefix),
                    dataset,
                    CancellationToken.None);

                Assert.True(again.SnapshotAlreadyImported);
            }

            // AC-03: polyhierarchy preserved. AC-03B: symmetric pair stored once.
            Assert.Equal(
                2,
                await ScalarAsync(
                    connectionString,
                    """
                    SELECT count(*) FROM genre_form_broader_relations r
                    JOIN genre_form_authority_terms t ON t.id = r.narrower_term_id
                    WHERE t.authority_uri = @uri
                    """,
                    ("uri", sermons)));

            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    """
                    SELECT count(*) FROM genre_form_related_relations r
                    JOIN genre_form_authority_terms a ON a.id = r.term_id_a
                    JOIN genre_form_authority_terms b ON b.id = r.term_id_b
                    WHERE a.authority_uri IN (@one, @two)
                      AND b.authority_uri IN (@one, @two)
                    """,
                    ("one", sermons),
                    ("two", creeds)));

            await ApproveAsync(connectionString, sermons, "selectable", 1);
            await ApproveAsync(connectionString, religious, "structural_only", null);

            // AC-06 and AC-07.
            await using (var context = new KnowledgeDbContext(options))
            {
                var store = new PostgreSqlGenreFormAuthorityStore(context);

                var selectable = await store.GetSelectableTermsAsync(CancellationToken.None);
                Assert.Contains(selectable, x => x.AuthorityUri == sermons);
                Assert.DoesNotContain(selectable, x => x.AuthorityUri == religious);

                // Narrower is derived by inverting the persisted broader relation.
                var narrower = await store.GetNarrowerTermsAsync(
                    religious,
                    CancellationToken.None);
                Assert.Contains(narrower, x => x.AuthorityUri == sermons);
                Assert.Contains(narrower, x => x.AuthorityUri == creeds);

                var broader = await store.GetBroaderTermsAsync(sermons, CancellationToken.None);
                Assert.Equal(2, broader.Count);

                var view = await store.GetTermByAuthorityUriAsync(
                    religious,
                    CancellationToken.None);
                Assert.Equal(GenreFormUsageStatus.StructuralOnly, view!.UsageStatus);
            }

            // AC-05 and AC-11: a refresh dropping a term keeps the editorial
            // decision and reports the affected entry instead of remapping it.
            await using (var context = new KnowledgeDbContext(options))
            {
                var store = new PostgreSqlGenreFormAuthorityStore(context);

                var refreshed = new GenreFormAuthorityDataset(
                [
                    Term(religious, "Religious works"),
                    Term(instructional, "Instructional works"),
                    Term(creeds, "Creeds", broader: [religious])
                ]);

                var result = await store.ImportAsync(
                    Snapshot("sha-two-" + prefix),
                    refreshed,
                    CancellationToken.None);

                Assert.False(result.SnapshotAlreadyImported);

                var review = Assert.Single(
                    result.ProfileReviewItems,
                    x => x.AuthorityUri == sermons);

                Assert.Equal(GenreFormUsageStatus.Selectable, review.UsageStatus);
                Assert.False(review.PresentInSnapshot);
            }

            await using (var context = new KnowledgeDbContext(options))
            {
                var store = new PostgreSqlGenreFormAuthorityStore(context);

                // The editorial decision survived the authority refresh.
                var selectable = await store.GetSelectableTermsAsync(CancellationToken.None);
                Assert.Contains(selectable, x => x.AuthorityUri == sermons);
            }
        }
        finally
        {
            await CleanupAsync(connectionString, prefix);
        }
    }

    [Fact]
    public async Task A_failed_import_leaves_no_partial_snapshot()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        var prefix = $"gf-{Guid.NewGuid():N}-";
        var orphan = Base + prefix + "orphan";
        var sha = "sha-fail-" + prefix;

        try
        {
            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlGenreFormAuthorityStore(context);

            var dataset = new GenreFormAuthorityDataset(
            [
                // Declares a broader term absent from the snapshot: fail closed
                // rather than invent a mapping.
                Term(orphan, "Orphan", broader: [Base + prefix + "missing"])
            ]);

            await Assert.ThrowsAsync<GenreFormAuthorityException>(
                () => store.ImportAsync(
                    Snapshot(sha),
                    dataset,
                    CancellationToken.None));

            Assert.Equal(
                0,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM genre_form_authority_snapshots WHERE content_sha256 = @sha",
                    ("sha", sha)));

            Assert.Equal(
                0,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM genre_form_authority_terms WHERE authority_uri = @uri",
                    ("uri", orphan)));
        }
        finally
        {
            await CleanupAsync(connectionString, prefix);
        }
    }

    [Fact]
    public async Task Legacy_source_kind_is_not_reinterpreted_as_a_genre_form()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        await PrepareAsync(connectionString);

        // AC-09: this increment never migrates the legacy vocabulary.
        Assert.Equal(
            0,
            await ScalarAsync(
                connectionString,
                """
                SELECT count(*) FROM knowledge_source_kinds k
                JOIN genre_form_authority_terms t ON lower(t.preferred_label) = lower(k.label)
                """));
    }

    private static GenreFormAuthorityTerm Term(
        string uri,
        string label,
        string[]? broader = null,
        string[]? related = null)
    {
        return new GenreFormAuthorityTerm(
            uri,
            uri[(uri.LastIndexOf('/') + 1)..],
            label,
            "en",
            GenreFormAuthorityStatus.Active,
            [],
            [],
            broader ?? [],
            related ?? []);
    }

    private static GenreFormAuthoritySnapshot Snapshot(string sha256)
    {
        return new GenreFormAuthoritySnapshot(
            "lcgft",
            "https://id.loc.gov/download/authorities/genreForms.skosrdf.jsonld.gz",
            sha256,
            new DateTimeOffset(2026, 9, 3, 20, 0, 0, TimeSpan.Zero),
            "integration-test");
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

    private static async Task ApproveAsync(
        string connectionString,
        string authorityUri,
        string usageStatus,
        int? displayOrder)
    {
        await ExecuteAsync(
            connectionString,
            """
            INSERT INTO genre_form_profile_entries
                (term_id, usage_status, display_order, profile_version, updated_at)
            SELECT id, @status, @order, 'integration-test-v1', now()
            FROM genre_form_authority_terms
            WHERE authority_uri = @uri
            """,
            ("status", usageStatus),
            ("order", (object?)displayOrder ?? DBNull.Value),
            ("uri", authorityUri));
    }

    private static async Task CleanupAsync(string connectionString, string prefix)
    {
        var pattern = Base + prefix + "%";

        await ExecuteAsync(
            connectionString,
            """
            DELETE FROM genre_form_profile_entries
            WHERE term_id IN (
                SELECT id FROM genre_form_authority_terms WHERE authority_uri LIKE @pattern);
            DELETE FROM genre_form_related_relations
            WHERE term_id_a IN (
                SELECT id FROM genre_form_authority_terms WHERE authority_uri LIKE @pattern)
               OR term_id_b IN (
                SELECT id FROM genre_form_authority_terms WHERE authority_uri LIKE @pattern);
            DELETE FROM genre_form_broader_relations
            WHERE narrower_term_id IN (
                SELECT id FROM genre_form_authority_terms WHERE authority_uri LIKE @pattern)
               OR broader_term_id IN (
                SELECT id FROM genre_form_authority_terms WHERE authority_uri LIKE @pattern);
            DELETE FROM genre_form_authority_terms WHERE authority_uri LIKE @pattern;
            """,
            ("pattern", pattern));
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
