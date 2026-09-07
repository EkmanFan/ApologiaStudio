using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.IntegrationTests.KnowledgeStore;
using ApologiaStudio.IntegrationTests.Persistence;
using ApologiaStudio.Web.FieldSuggestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.FieldSuggestions;

/// <summary>
/// The automatic Genre/Form analysis that follows a draft's creation.
/// </summary>
/// <remarks>
/// Driven through the real background service and the real Postgres stores, so
/// what is verified is the wiring rather than a rehearsal of it. The encoder
/// itself is scripted here; the run against real models is the opt-in smoke
/// below.
/// </remarks>
[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class GenreFormAutoAnalysisTests
{
    #region Methods

    [Fact]
    public async Task A_new_draft_is_analysed_without_anyone_asking()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            await RunAsync(
                options,
                draftId,
                Scripted.Succeeding(("apologetic_writing", 0.93)));

            var analysis = await CurrentAsync(options, draftId);

            Assert.NotNull(analysis);
            Assert.Equal(MetadataReviewAnalysisStatus.Valid, analysis.Status);
            Assert.Equal("encoder", analysis.ModelProvider);
            Assert.Equal(
                GenreFormAnalysisHostedService.AutomaticActorId,
                analysis.ActorUserId);

            var suggestion = Assert.Single(analysis.SuggestedTerms);
            Assert.Equal("apologetic_writing", suggestion.Code);
            Assert.Equal(0.93, suggestion.Score);
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_completed_analysis_with_no_term_is_still_recorded()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            await RunAsync(options, draftId, Scripted.NoSuggestion());

            var analysis = await CurrentAsync(options, draftId);

            Assert.NotNull(analysis);
            Assert.Equal(MetadataReviewAnalysisStatus.Valid, analysis.Status);
            Assert.Empty(analysis.SuggestedTerms);
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task An_unavailable_encoder_leaves_the_draft_and_history_alone()
    {
        // The worker may still be loading its models when the first document
        // arrives. That must cost nothing: no history, no blocked draft.
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            await RunAsync(
                options,
                draftId,
                new UnavailableFieldSuggestionProvider());

            Assert.Null(await CurrentAsync(options, draftId));

            // The draft is intact and reviewable.
            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM document_manager_editorial_drafts " +
                    $"WHERE id = '{draftId:D}';"));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_failed_analysis_is_recorded_and_the_draft_survives()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            // An impossible product code: the provider broke the contract.
            await RunAsync(
                options,
                draftId,
                Scripted.Succeeding(("not_a_genre", 0.9)));

            var analysis = await CurrentAsync(options, draftId);

            Assert.NotNull(analysis);
            Assert.Equal(MetadataReviewAnalysisStatus.Failed, analysis.Status);
            Assert.Empty(analysis.SuggestedTerms);

            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM document_manager_editorial_drafts " +
                    $"WHERE id = '{draftId:D}';"));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task An_already_analysed_draft_is_not_analysed_again()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            var provider = Scripted.Succeeding(("sermon", 0.9));

            await RunAsync(options, draftId, provider);
            Assert.Equal(1, provider.Calls);

            // A replayed Manager result must not produce a second analysis.
            await RunAsync(options, draftId, provider);
            Assert.Equal(1, provider.Calls);

            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM metadata_review_analyses " +
                    $"WHERE draft_id = '{draftId:D}';"));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_reviewer_can_still_re_run_after_changing_the_evidence()
    {
        // The anti-duplicate rule guards the automatic path only. An explicit
        // request always runs, or a corrected title could never be re-analysed.
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        try
        {
            await RunAsync(options, draftId, Scripted.Succeeding(("sermon", 0.9)));

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlDocumentManagerEditorialReviewStore(context);
            var draft = (await store.GetAsync(draftId, CancellationToken.None))!;

            var service = new GenreFormFieldSuggestionService(
                Scripted.Succeeding(("commentary", 0.71)),
                new PostgreSqlMetadataReviewAnalysisStore(context));

            var analysis = await service.AnalyzeAndRecordAsync(
                draft with { Title = "Commentaire sur les Psaumes" },
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.Equal(FieldSuggestionStatus.Succeeded, analysis.Status);

            var current = await CurrentAsync(options, draftId);
            Assert.Equal(
                "commentary",
                Assert.Single(current!.SuggestedTerms).Code);

            // History is append-only: the first run is superseded, not erased.
            Assert.Equal(
                2,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM metadata_review_analyses " +
                    $"WHERE draft_id = '{draftId:D}';"));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task The_real_encoder_analyses_a_new_draft_end_to_end()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = await PrepareAsync(connectionString);
        var draftId = await SeedDraftAsync(connectionString);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        var provider = new EncoderBackedFieldSuggestionProvider(
            new GenreFormInferencePlan(
                new HttpEncoderInferenceRuntime(
                    httpClient,
                    new EncoderInferenceOptions(
                        LiveEncoderIntegrationGate.Endpoint(),
                        TimeSpan.FromSeconds(60)))));

        try
        {
            await RunAsync(options, draftId, provider);

            var analysis = await CurrentAsync(options, draftId);

            Assert.NotNull(analysis);
            Assert.Equal(MetadataReviewAnalysisStatus.Valid, analysis.Status);
            Assert.Equal("genre-form-cascade/v2.1", analysis.PlanId);
            Assert.StartsWith(
                "sha256:",
                analysis.ModelVersion,
                StringComparison.Ordinal);

            Assert.All(
                analysis.SuggestedTerms,
                x =>
                {
                    Assert.Contains(x.Code, GenreFormEncoderLabelMap.OutputOrder);
                    Assert.NotNull(x.Score);
                });
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    #endregion

    #region Methods Helpers

    /// <summary>
    /// Runs the real background service over one queued draft.
    /// </summary>
    private static async Task RunAsync(
        DbContextOptions<KnowledgeDbContext> options,
        Guid draftId,
        IFieldSuggestionProvider provider)
    {
        var services = new ServiceCollection();

        services.AddScoped(_ => new KnowledgeDbContext(options));
        services.AddScoped<
            IDocumentManagerEditorialReviewStore,
            PostgreSqlDocumentManagerEditorialReviewStore>();
        services.AddScoped<
            IMetadataReviewAnalysisStore,
            PostgreSqlMetadataReviewAnalysisStore>();
        services.AddScoped(_ => provider);
        services.AddScoped<GenreFormFieldSuggestionService>();

        var queue = new GenreFormAnalysisQueue();

        var service = new GenreFormAnalysisHostedService(
            queue,
            services.BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>(),
            NullLogger<GenreFormAnalysisHostedService>.Instance);

        queue.TryEnqueue(draftId);

        await service.StartAsync(CancellationToken.None);

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(2);

        while (DateTimeOffset.UtcNow < deadline &&
               !queue.TryEnqueue(draftId))
        {
            // The draft is released once its analysis has been attempted.
            await Task.Delay(50);
        }

        queue.Release(draftId);

        await service.StopAsync(CancellationToken.None);
    }

    private static async Task<MetadataReviewAnalysis?> CurrentAsync(
        DbContextOptions<KnowledgeDbContext> options,
        Guid draftId)
    {
        await using var context = new KnowledgeDbContext(options);

        return await new PostgreSqlMetadataReviewAnalysisStore(context)
            .GetCurrentAsync(
                draftId,
                MetadataReviewAnalysis.GenreFormField,
                CancellationToken.None);
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
        var sha = new string('e', 64);

        await ExecuteAsync(
            connectionString,
            $"""
             INSERT INTO document_manager_submission_manifest_inbox
                 (submission_id, revision, source_sha256, original_file_name,
                  finalized_at_utc)
             VALUES ('{submissionId:D}', 1, '{sha}', 'auto.pdf', now());

             INSERT INTO document_manager_editorial_drafts
                 (id, submission_id, manifest_revision, source_sha256,
                  original_file_name, title, title_origin, description,
                  status, version, created_at_utc, updated_at_utc)
             VALUES ('{draftId:D}', '{submissionId:D}', 1, '{sha}',
                     'auto.pdf', 'Réponse aux objections contre la foi',
                     'imported', 'Défense raisonnée de la doctrine.',
                     'pending_review', 0, now(), now());
             """);

        return draftId;
    }

    private static Task CleanupAsync(string connectionString, Guid draftId) =>
        ExecuteAsync(
            connectionString,
            $"""
             DELETE FROM document_manager_editorial_draft_genre_forms
             WHERE draft_id = '{draftId:D}';
             DELETE FROM metadata_review_analyses WHERE draft_id = '{draftId:D}';
             DELETE FROM document_manager_editorial_drafts WHERE id = '{draftId:D}';
             """);

    private static async Task<int> ScalarAsync(
        string connectionString,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class Scripted(
        FieldSuggestionStatus status,
        IReadOnlyList<(string Code, double Score)> values)
        : IFieldSuggestionProvider
    {
        public int Calls { get; private set; }

        public static Scripted Succeeding(
            params (string Code, double Score)[] values) =>
            new(FieldSuggestionStatus.Succeeded, values);

        public static Scripted NoSuggestion() =>
            new(FieldSuggestionStatus.NoSuggestion, []);

        public Task<FieldSuggestionBatch> GetSuggestionsAsync(
            FieldSuggestionRequest request,
            IReadOnlyCollection<ApologiaFieldId>? fields = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult(
                new FieldSuggestionBatch(
                [
                    new FieldSuggestionResult(
                        ApologiaFieldId.GenreForm,
                        status,
                        values
                            .Select(x => new FieldSuggestion(x.Code, x.Score))
                            .ToList(),
                        new FieldSuggestionProvenance(
                            "encoder",
                            "genre-form-cascade/v2.1",
                            "xlm-roberta-large",
                            "sha256:test"))
                ]));
        }
    }

    #endregion
}
