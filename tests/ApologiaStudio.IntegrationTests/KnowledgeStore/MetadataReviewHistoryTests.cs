using ApologiaStudio.Application.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Knowledge.MetadataReview;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using ApologiaStudio.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.KnowledgeStore;

/// <summary>
/// Analysis history is advisory: it records what the assistant proposed and
/// what the reviewer decided, and never carries authoritative metadata.
/// </summary>
[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class MetadataReviewHistoryTests
{
    private const string Base = "http://id.loc.gov/authorities/genreForms/";

    private static readonly Guid Actor =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task An_accepted_suggestion_is_reconstructable()
    {
        var (options, connectionString, draftId) = await PrepareAsync();

        try
        {
            var apologetic = await UriForAsync(connectionString, "Apologetic writings");

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlMetadataReviewAnalysisStore(context);

            var analysis = await store.RecordAsync(
                Command(draftId, Suggestion(apologetic, "Sustained defence.")),
                CancellationToken.None);

            Assert.Equal(MetadataReviewAnalysisStatus.Valid, analysis.Status);
            Assert.Equal("apologia-genre-form-profile-v1", analysis.PolicyVersion);
            Assert.Equal("ollama", analysis.ModelProvider);

            var suggestion = Assert.Single(analysis.SuggestedTerms);
            Assert.Equal("Apologetic writings", suggestion.PreferredLabel);
            Assert.Equal("introduction, p. 3", Assert.Single(suggestion.Evidence));

            // The reviewer confirmed exactly what was proposed.
            await store.RecordReviewerOutcomeAsync(
                analysis.Id,
                MetadataReviewOutcomeCalculator.Determine(
                    [apologetic],
                    [apologetic]),
                Actor,
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            var current = await store.GetCurrentAsync(
                draftId,
                MetadataReviewAnalysis.GenreFormField,
                CancellationToken.None);

            Assert.Equal(MetadataReviewOutcome.Accepted, current!.ReviewerOutcome);
            Assert.Equal(Actor, current.ReviewerUserId);
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_modified_and_a_rejected_outcome_are_distinguished()
    {
        var (options, connectionString, draftId) = await PrepareAsync();

        try
        {
            var apologetic = await UriForAsync(connectionString, "Apologetic writings");
            var essays = await UriForAsync(connectionString, "Essays");
            var textbooks = await UriForAsync(connectionString, "Textbooks");

            // The reviewer kept one term and added another.
            Assert.Equal(
                MetadataReviewOutcome.Modified,
                MetadataReviewOutcomeCalculator.Determine(
                    [apologetic],
                    [apologetic, essays]));

            // The reviewer kept nothing that was proposed.
            Assert.Equal(
                MetadataReviewOutcome.Rejected,
                MetadataReviewOutcomeCalculator.Determine(
                    [apologetic],
                    [textbooks]));

            // Proposing nothing and confirming nothing is agreement.
            Assert.Equal(
                MetadataReviewOutcome.Accepted,
                MetadataReviewOutcomeCalculator.Determine([], []));

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlMetadataReviewAnalysisStore(context);

            var analysis = await store.RecordAsync(
                Command(draftId, Suggestion(apologetic, "Defence of a position.")),
                CancellationToken.None);

            await store.RecordReviewerOutcomeAsync(
                analysis.Id,
                MetadataReviewOutcome.Rejected,
                Actor,
                DateTimeOffset.UtcNow,
                CancellationToken.None);

            var stored = await store.GetCurrentAsync(
                draftId,
                MetadataReviewAnalysis.GenreFormField,
                CancellationToken.None);

            Assert.Equal(MetadataReviewOutcome.Rejected, stored!.ReviewerOutcome);
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_zero_suggestion_analysis_is_recorded_as_insufficient_evidence()
    {
        var (options, connectionString, draftId) = await PrepareAsync();

        try
        {
            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlMetadataReviewAnalysisStore(context);

            var analysis = await store.RecordAsync(
                new RecordMetadataReviewAnalysisCommand(
                    draftId,
                    Actor,
                    new GenreFormClassificationResult(
                        Identity(),
                        [],
                        [],
                        InsufficientEvidence: true),
                    DateTimeOffset.UtcNow.AddSeconds(-2),
                    DateTimeOffset.UtcNow,
                    2000),
                CancellationToken.None);

            Assert.Empty(analysis.Suggestions);
            Assert.True(analysis.InsufficientEvidence);
            Assert.Equal(MetadataReviewAnalysisStatus.Valid, analysis.Status);
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_regenerated_analysis_supersedes_without_erasing_history()
    {
        var (options, connectionString, draftId) = await PrepareAsync();

        try
        {
            var apologetic = await UriForAsync(connectionString, "Apologetic writings");
            var essays = await UriForAsync(connectionString, "Essays");

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlMetadataReviewAnalysisStore(context);

            var first = await store.RecordAsync(
                Command(draftId, Suggestion(apologetic, "First reading.")),
                CancellationToken.None);

            var second = await store.RecordAsync(
                Command(draftId, Suggestion(essays, "Second reading.")),
                CancellationToken.None);

            var current = await store.GetCurrentAsync(
                draftId,
                MetadataReviewAnalysis.GenreFormField,
                CancellationToken.None);

            Assert.Equal(second.Id, current!.Id);

            var history = await store.ListAsync(
                draftId,
                MetadataReviewAnalysis.GenreFormField,
                CancellationToken.None);

            Assert.Equal(2, history.Count);

            // The earlier run is still readable, and points at its successor.
            var superseded = Assert.Single(history, x => x.Id == first.Id);
            Assert.Equal(second.Id, superseded.SupersededByAnalysisId);
            Assert.Equal(
                "Apologetic writings",
                Assert.Single(superseded.SuggestedTerms).PreferredLabel);
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task A_failed_analysis_records_a_diagnostic_and_no_suggestion()
    {
        var (options, connectionString, draftId) = await PrepareAsync();

        try
        {
            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlMetadataReviewAnalysisStore(context);

            var failed = await store.RecordFailureAsync(
                new RecordFailedMetadataReviewAnalysisCommand(
                    draftId,
                    Actor,
                    "'gf9999999999' is not a term of the active profile.",
                    "apologia-genre-form-profile-v1",
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    1000),
                CancellationToken.None);

            Assert.Equal(MetadataReviewAnalysisStatus.Failed, failed.Status);
            Assert.Empty(failed.Suggestions);
            Assert.Contains("not a term", failed.FailureReason!);
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    [Fact]
    public async Task Editorial_genre_form_state_survives_independently_of_history()
    {
        var (options, connectionString, draftId) = await PrepareAsync();

        try
        {
            var apologetic = await UriForAsync(connectionString, "Apologetic writings");

            // A reviewer selection exists with no analysis at all: manual
            // review never depends on the assistant.
            await ExecuteAsync(
                connectionString,
                """
                INSERT INTO document_manager_editorial_draft_genre_forms (draft_id, term_id)
                SELECT @draft, id FROM genre_form_authority_terms WHERE authority_uri = @uri
                """,
                ("draft", draftId),
                ("uri", apologetic));

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlMetadataReviewAnalysisStore(context);

            Assert.Null(
                await store.GetCurrentAsync(
                    draftId,
                    MetadataReviewAnalysis.GenreFormField,
                    CancellationToken.None));

            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM document_manager_editorial_draft_genre_forms " +
                    "WHERE draft_id = @draft",
                    ("draft", draftId)));

            // Recording history afterwards leaves the editorial state alone.
            await store.RecordAsync(
                Command(draftId, Suggestion(apologetic, "Defence of a position.")),
                CancellationToken.None);

            Assert.Equal(
                1,
                await ScalarAsync(
                    connectionString,
                    "SELECT count(*) FROM document_manager_editorial_draft_genre_forms " +
                    "WHERE draft_id = @draft",
                    ("draft", draftId)));
        }
        finally
        {
            await CleanupAsync(connectionString, draftId);
        }
    }

    private static RecordMetadataReviewAnalysisCommand Command(
        Guid draftId,
        GenreFormSuggestion suggestion)
    {
        return new RecordMetadataReviewAnalysisCommand(
            draftId,
            Actor,
            new GenreFormClassificationResult(
                Identity(),
                [suggestion],
                [],
                InsufficientEvidence: false),
            DateTimeOffset.UtcNow.AddSeconds(-3),
            DateTimeOffset.UtcNow,
            3000);
    }

    private static GenreFormSuggestion Suggestion(
        string authorityUri,
        string justification)
    {
        return new GenreFormSuggestion(
            authorityUri,
            authorityUri[(authorityUri.LastIndexOf('/') + 1)..],
            "ignored",
            justification,
            ["introduction, p. 3"]);
    }

    private static MetadataReviewAnalysisIdentity Identity()
    {
        return new MetadataReviewAnalysisIdentity(
            "apologia-genre-form-profile-v1",
            "genre-form-classification/1",
            "ollama",
            "qwen3:8b",
            DateTimeOffset.UtcNow);
    }

    private static async Task<(
        DbContextOptions<KnowledgeDbContext> Options,
        string ConnectionString,
        Guid DraftId)> PrepareAsync()
    {
        var connectionString = KnowledgeStoreTestConnection.Resolve();
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, builder => builder.UseVector())
            .Options;

        await using (var context = new KnowledgeDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        await EnsureAuthorityAsync(options);

        var draftId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        await ExecuteAsync(
            connectionString,
            """
            INSERT INTO document_manager_submission_manifest_inbox
                (submission_id, revision, source_sha256, original_file_name,
                 finalized_at_utc)
            VALUES (@submission, 1, @sha, 'history.pdf', now())
            """,
            ("submission", submissionId),
            ("sha", new string('c', 64)));

        await ExecuteAsync(
            connectionString,
            """
            INSERT INTO document_manager_editorial_drafts
                (id, submission_id, manifest_revision, source_sha256,
                 original_file_name, title, title_origin, status, version,
                 created_at_utc, updated_at_utc)
            VALUES (@id, @submission, 1, @sha, 'history.pdf', 'History test',
                    'editorial', 'pending_review', 0, now(), now())
            """,
            ("id", draftId),
            ("submission", submissionId),
            ("sha", new string('c', 64)));

        return (options, connectionString, draftId);
    }

    private static async Task EnsureAuthorityAsync(
        DbContextOptions<KnowledgeDbContext> options)
    {
        await using var context = new KnowledgeDbContext(options);

        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "lcgft-profile-v1-fixture.jsonl");

        var payload = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();

        using var content = new MemoryStream(payload, writable: false);
        var dataset = new Infrastructure.Knowledge.GenreForms
            .SkosJsonLdGenreFormDatasetReader().Read(content);

        await new Infrastructure.Knowledge.GenreForms
            .PostgreSqlGenreFormAuthorityStore(context).ImportAsync(
                new Application.Knowledge.GenreForms.GenreFormAuthoritySnapshot(
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

    private static async Task CleanupAsync(string connectionString, Guid draftId)
    {
        await ExecuteAsync(
            connectionString,
            """
            DELETE FROM metadata_review_suggestion_evidence
            WHERE suggestion_id IN (
                SELECT s.id FROM metadata_review_suggestions s
                JOIN metadata_review_analyses a ON a.id = s.analysis_id
                WHERE a.draft_id = @draft);
            DELETE FROM metadata_review_suggestions
            WHERE analysis_id IN (
                SELECT id FROM metadata_review_analyses WHERE draft_id = @draft);
            UPDATE metadata_review_analyses SET superseded_by_analysis_id = NULL
            WHERE draft_id = @draft;
            DELETE FROM metadata_review_analyses WHERE draft_id = @draft;
            DELETE FROM document_manager_editorial_draft_genre_forms WHERE draft_id = @draft;
            DELETE FROM document_manager_editorial_drafts WHERE id = @draft;
            DELETE FROM document_manager_submission_manifest_inbox
            WHERE submission_id IN (
                SELECT submission_id FROM document_manager_editorial_drafts
                WHERE id = @draft);
            """,
            ("draft", draftId));
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
