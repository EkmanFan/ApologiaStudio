using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

/// <summary>
/// The review side of the Genre/Form cutover: the reviewer's draft selection
/// and the assistant's suggestions.
/// </summary>
/// <remarks>
/// Both now carry Apologia product terms. These verify the invariants the
/// cutover had to preserve, the shape of the two relations, and that the
/// vocabulary a reviewer sees no longer needs an LCGFT import to exist.
/// </remarks>
[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class ReviewGenreFormCutoverTests
{
    #region Methods Draft Selection

    [Fact]
    public async Task A_draft_may_carry_no_genre_form()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            Assert.Equal(0, await DraftSelectionCountAsync(connectionString, draftId));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_draft_carries_one_or_many_product_terms()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            await SelectAsync(connectionString, draftId, "sermon");
            Assert.Equal(1, await DraftSelectionCountAsync(connectionString, draftId));

            await SelectAsync(connectionString, draftId, "commentary");
            await SelectAsync(connectionString, draftId, "study_guide");

            Assert.Equal(3, await DraftSelectionCountAsync(connectionString, draftId));

            // Read back in product display order, not in selection order.
            Assert.Equal(
                ["commentary", "sermon", "study_guide"],
                await SelectedCodesAsync(connectionString, draftId));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task The_same_draft_pair_is_never_persisted_twice()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            await SelectAsync(connectionString, draftId, "prayer");

            await Assert.ThrowsAsync<PostgresException>(
                () => SelectAsync(connectionString, draftId, "prayer"));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_draft_cannot_select_a_term_outside_the_product_taxonomy()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(
                    connectionString,
                    "INSERT INTO document_manager_editorial_draft_genre_forms " +
                    "(draft_id, product_term_id) VALUES " +
                    $"('{draftId:D}', " +
                    "'00000000-0000-0000-0000-0000000000ff');"));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    #endregion

    #region Methods Suggestions

    [Fact]
    public async Task A_suggestion_carries_a_product_identity()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            var analysisId = await SeedAnalysisAsync(connectionString, draftId);

            await SuggestAsync(connectionString, analysisId, "apologetic_writing");

            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM metadata_review_suggestions s " +
                    "JOIN apologia_genre_form_terms t " +
                    "  ON t.id = s.product_term_id " +
                    $"WHERE s.analysis_id = '{analysisId:D}' " +
                    "AND t.code = 'apologetic_writing';"));

            // The same term twice in one analysis is refused.
            await Assert.ThrowsAsync<PostgresException>(
                () => SuggestAsync(
                    connectionString,
                    analysisId,
                    "apologetic_writing"));

            // An identity outside the product taxonomy is refused.
            await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(
                    connectionString,
                    "INSERT INTO metadata_review_suggestions " +
                    "(analysis_id, product_term_id, disposition, justification) " +
                    $"VALUES ('{analysisId:D}', " +
                    "'00000000-0000-0000-0000-0000000000ff', " +
                    "'suggested', 'x');"));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    #endregion

    #region Methods Vocabulary

    [Fact]
    public async Task The_reviewer_vocabulary_is_the_twenty_seven_product_terms()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        await using var context = new KnowledgeDbContext(options);

        var policy = await new KnowledgeStoreGenreFormPolicyProvider(context)
            .GetActivePolicyAsync(CancellationToken.None);

        Assert.Equal(ApologiaGenreFormTaxonomy.Version, policy.TaxonomyVersion);
        Assert.Equal(27, policy.Terms.Count);
        Assert.Equal(24, policy.EncoderPredictableTerms.Count());

        // The three manual-only terms are offered like any other: prediction
        // mode bounds the encoder, never the reviewer.
        Assert.All(
            new[] { "study_guide", "training_material", "instructional_lesson" },
            code => Assert.Equal(
                GenreFormPredictionMode.ManualOnly,
                policy.Find(code)!.PredictionMode));

        // Product display order, not authority label order.
        Assert.Equal(
            ApologiaGenreFormTaxonomy.Terms.Select(x => x.Code),
            policy.Terms.Select(x => x.Code));
    }

    [Fact]
    public async Task The_vocabulary_does_not_depend_on_the_authority_catalogue()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);

        // Whatever the catalogue holds, the policy is the product taxonomy.
        // A reviewer keeps a full vocabulary even if LCGFT was never imported.
        await using var context = new KnowledgeDbContext(options);

        var policy = await new KnowledgeStoreGenreFormPolicyProvider(context)
            .GetActivePolicyAsync(CancellationToken.None);

        Assert.DoesNotContain(
            policy.Terms,
            x => x.Code.StartsWith("gf", StringComparison.Ordinal) ||
                 x.Code.Contains("id.loc.gov", StringComparison.Ordinal));
    }

    #endregion

    #region Methods Authoritative Save

    [Fact]
    public async Task The_reviewer_selection_reaches_the_work_without_conversion()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);
        var workId = Guid.NewGuid();

        try
        {
            await SelectAsync(connectionString, draftId, "catechism");
            await SelectAsync(connectionString, draftId, "creed");

            var selected = await SelectedCodesAsync(connectionString, draftId);

            await SeedWorkAsync(connectionString, workId);

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlWorkGenreFormAssignmentStore(context);

            foreach (var code in selected)
            {
                Assert.True(
                    (await store.AssignAsync(
                        workId,
                        code,
                        CancellationToken.None)).Assigned);
            }

            // The same codes travel end to end: no ProductTerm → LCGFT → Work
            // conversion happens anywhere along the way.
            Assert.Equal(
                selected.OrderBy(x => x, StringComparer.Ordinal),
                (await store.GetWorkGenreFormsAsync(workId, CancellationToken.None))
                    .Select(x => x.Code)
                    .OrderBy(x => x, StringComparer.Ordinal));

            Assert.Equal(
                2,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM knowledge_work_genre_forms w " +
                    "JOIN apologia_genre_form_terms t " +
                    "  ON t.id = w.product_term_id " +
                    $"WHERE w.work_id = '{workId:D}';"));
        }
        finally
        {
            await ExecuteAsync(
                connectionString,
                $"""
                 DELETE FROM knowledge_work_genre_forms WHERE work_id = '{workId:D}';
                 DELETE FROM knowledge_works WHERE id = '{workId:D}';
                 DELETE FROM knowledge_resources WHERE id = '{workId:D}';
                 """);
            await CleanupAsync(connectionString, draftId);
        }
    }

    #endregion

    #region Methods Regression

    [Fact]
    public async Task The_authority_catalogue_and_its_mappings_are_untouched()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        var termsBefore = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM genre_form_authority_terms;");
        var mappingsBefore = await ScalarAsync(
            connectionString,
            "SELECT count(*) FROM genre_form_authority_mappings;");

        try
        {
            await SelectAsync(connectionString, draftId, "sermon");

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
            await CleanupAsync(connectionString, draftId);
        }
    }

    #endregion

    #region Methods Helpers

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

    private static async Task<Guid> SeedDraftAsync(string connectionString)
    {
        var draftId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var sha = new string('d', 64);

        await ExecuteAsync(
            connectionString,
            $"""
             INSERT INTO document_manager_submission_manifest_inbox
                 (submission_id, revision, source_sha256, original_file_name,
                  finalized_at_utc)
             VALUES ('{submissionId:D}', 1, '{sha}', 'cutover.pdf', now());

             INSERT INTO document_manager_editorial_drafts
                 (id, submission_id, manifest_revision, source_sha256,
                  original_file_name, title, title_origin, status, version,
                  created_at_utc, updated_at_utc)
             VALUES ('{draftId:D}', '{submissionId:D}', 1, '{sha}',
                     'cutover.pdf', 'Cutover draft', 'editorial',
                     'pending_review', 0, now(), now());
             """);

        return draftId;
    }

    private static async Task SeedWorkAsync(
        string connectionString,
        Guid workId)
    {
        await ExecuteAsync(
            connectionString,
            $"""
             INSERT INTO knowledge_resources (id, editorial_review_status, created_at)
             VALUES ('{workId:D}', 'approved', now())
             ON CONFLICT (id) DO NOTHING;
             INSERT INTO knowledge_works (id, title)
             VALUES ('{workId:D}', 'Cutover work')
             ON CONFLICT (id) DO NOTHING;
             """);
    }

    private static async Task<Guid> SeedAnalysisAsync(
        string connectionString,
        Guid draftId)
    {
        var analysisId = Guid.CreateVersion7();

        await ExecuteAsync(
            connectionString,
            "INSERT INTO metadata_review_analyses " +
            "(id, draft_id, field, status, policy_version, " +
            " insufficient_evidence, requested_at_utc, completed_at_utc, " +
            " actor_user_id) VALUES " +
            $"('{analysisId:D}', '{draftId:D}', 'genre_form', 'valid', " +
            $"'{ApologiaGenreFormTaxonomy.Version}', false, now(), now(), " +
            "'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');");

        return analysisId;
    }

    private static Task SelectAsync(
        string connectionString,
        Guid draftId,
        string termCode) =>
        ExecuteAsync(
            connectionString,
            "INSERT INTO document_manager_editorial_draft_genre_forms " +
            "(draft_id, product_term_id) " +
            $"SELECT '{draftId:D}', id FROM apologia_genre_form_terms " +
            $"WHERE code = '{termCode}';");

    private static Task SuggestAsync(
        string connectionString,
        Guid analysisId,
        string termCode) =>
        ExecuteAsync(
            connectionString,
            "INSERT INTO metadata_review_suggestions " +
            "(analysis_id, product_term_id, disposition, justification) " +
            $"SELECT '{analysisId:D}', id, 'suggested', 'because' " +
            $"FROM apologia_genre_form_terms WHERE code = '{termCode}';");

    private static Task<int> DraftSelectionCountAsync(
        string connectionString,
        Guid draftId) =>
        ScalarAsync(
            connectionString,
            "SELECT count(*) FROM document_manager_editorial_draft_genre_forms " +
            $"WHERE draft_id = '{draftId:D}';");

    private static async Task<IReadOnlyList<string>> SelectedCodesAsync(
        string connectionString,
        Guid draftId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT t.code FROM document_manager_editorial_draft_genre_forms s " +
            "JOIN apologia_genre_form_terms t ON t.id = s.product_term_id " +
            $"WHERE s.draft_id = '{draftId:D}' ORDER BY t.display_order;",
            connection);

        var codes = new List<string>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            codes.Add(reader.GetString(0));
        }

        return codes;
    }

    private static async Task CleanupAsync(
        string connectionString,
        Guid draftId)
    {
        await ExecuteAsync(
            connectionString,
            $"""
             DELETE FROM document_manager_editorial_draft_genre_forms
             WHERE draft_id = '{draftId:D}';
             DELETE FROM metadata_review_analyses WHERE draft_id = '{draftId:D}';
             DELETE FROM document_manager_editorial_drafts WHERE id = '{draftId:D}';
             """);
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
