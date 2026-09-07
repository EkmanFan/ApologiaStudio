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
    public async Task Authority_import_is_idempotent_and_preserves_apologia_alignments()
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
                Assert.Empty(first.AuthorityReviewItems);
            }

            // AC-14: importing a concept creates no local usage of it. The
            // product taxonomy is untouched and no alignment appears.
            Assert.Equal(
                27,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM apologia_genre_form_terms"));
            Assert.Equal(
                0,
                await ScalarAsync(
                    connectionString,
                    """
                    SELECT count(*) FROM genre_form_authority_mappings m
                    WHERE m.external_concept_id LIKE @pattern
                    """,
                    ("pattern", prefix + "%")));

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

            // An Apologia product term is aligned to one imported concept.
            // That alignment is now the only local dependency on the catalogue.
            await AlignAsync(connectionString, prefix + "sermons", "sermon");

            // AC-06 and AC-07: hierarchy reads stay available on the catalogue.
            await using (var context = new KnowledgeDbContext(options))
            {
                var store = new PostgreSqlGenreFormAuthorityStore(context);

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
                Assert.Equal("Religious works", view!.PreferredLabel);
                Assert.Equal(GenreFormAuthorityStatus.Active, view.Status);
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
                    result.AuthorityReviewItems,
                    x => x.AuthorityUri == sermons);

                // The report names the product term whose alignment is at risk,
                // which is what a human actually has to decide about.
                Assert.Equal(["sermon"], review.AlignedProductTermCodes);
                Assert.False(review.PresentInSnapshot);
            }

            // The alignment survived the authority refresh: it is reported,
            // never silently remapped or dropped.
            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    """
                    SELECT count(*) FROM genre_form_authority_mappings
                    WHERE external_concept_id = @concept
                    """,
                    ("concept", prefix + "sermons")));
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

    [Fact]
    public async Task Recognized_variants_resolve_to_the_authorized_term()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await EnsureLcgftImportedAsync(options);

        // AC-GF-09 / GF-RULE-07: a variant is never a second Genre/Form value.
        // This is a catalogue fact and stays true now that the product no
        // longer selects from LCGFT.
        Assert.Equal(
            "Sermons",
            await LabelForVariantAsync(connectionString, "Homilies"));
        Assert.Equal(
            "Creeds",
            await LabelForVariantAsync(connectionString, "Confessions of faith"));
    }

    /// <summary>
    /// Imports the pinned subset of the official LCGFT dataset.
    /// </summary>
    private static async Task EnsureLcgftImportedAsync(
        DbContextOptions<KnowledgeDbContext> options)
    {
        await using var context = new KnowledgeDbContext(options);

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
            WHERE v.label = @variant AND t.authority = 'lcgft'
            """,
            connection);
        command.Parameters.AddWithValue("variant", variant);

        return (string)(await command.ExecuteScalarAsync())!;
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
        // Synthetic terms are imported under the BnF authority: an import
        // replaces one authority's facts wholesale, and driving that against
        // 'lcgft' would wipe the real catalogue other tests depend on. BnF
        // carries no real data, and the alignment model was built to accept it.
        return new GenreFormAuthoritySnapshot(
            "bnf",
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

        await using (var context = new KnowledgeDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        // The alignment used below points at a real product term.
        await using (var context = new KnowledgeDbContext(options))
        {
            await new PostgreSqlApologiaGenreFormTaxonomySeeder(
                    context,
                    TimeProvider.System)
                .ApplyAsync(CancellationToken.None);
        }

        return options;
    }

    /// <summary>
    /// Records a synthetic alignment from a real product term to one imported
    /// concept, so the refresh report has a genuine local dependency to find.
    /// </summary>
    private static async Task AlignAsync(
        string connectionString,
        string externalConceptId,
        string productTermCode)
    {
        await ExecuteAsync(
            connectionString,
            """
            INSERT INTO genre_form_authority_mappings
                (id, product_term_id, authority, external_concept_id,
                 external_concept_uri, mapping_kind, updated_at)
            SELECT gen_random_uuid(), id, 'bnf', @concept, NULL, 'exact', now()
            FROM apologia_genre_form_terms
            WHERE code = @code
            """,
            ("concept", externalConceptId),
            ("code", productTermCode));
    }

    private static async Task CleanupAsync(string connectionString, string prefix)
    {
        var pattern = Base + prefix + "%";

        await ExecuteAsync(
            connectionString,
            """
            DELETE FROM genre_form_authority_mappings
            WHERE external_concept_id LIKE @concepts;
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
            ("pattern", pattern),
            ("concepts", prefix + "%"));
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
