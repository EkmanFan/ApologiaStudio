using System.Security.Cryptography;
using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;
using ApologiaStudio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlBibleCorpusImporterTests
{
    private static readonly string[] ProtestantBookCodes =
    [
        "GEN", "EXO", "LEV", "NUM", "DEU", "JOS", "JDG", "RUT", "1SA", "2SA",
        "1KI", "2KI", "1CH", "2CH", "EZR", "NEH", "EST", "JOB", "PSA", "PRO",
        "ECC", "SNG", "ISA", "JER", "LAM", "EZK", "DAN", "HOS", "JOL", "AMO",
        "OBA", "JON", "MIC", "NAM", "HAB", "ZEP", "HAG", "ZEC", "MAL", "MAT",
        "MRK", "LUK", "JHN", "ACT", "ROM", "1CO", "2CO", "GAL", "EPH", "PHP",
        "COL", "1TH", "2TH", "1TI", "2TI", "TIT", "PHM", "HEB", "JAS", "1PE",
        "2PE", "1JN", "2JN", "3JN", "JUD", "REV"
    ];

    [Fact]
    public async Task Import_ShouldBeIdempotentAndAtomicallyActivateNewVersion()
    {
        await using var context = CreateContext();
        await ResetDatabaseAsync(context);
        using var corpusDirectory = CreateCorpusDirectory("first-source");

        var reader = new StubCorpusReader(CreateParsedCorpus(corpusDirectory.Path));
        var importer = new PostgreSqlBibleCorpusImporter(
            context,
            reader,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 2, 20, 0, 0, TimeSpan.Zero)));

        var firstRequest = CreateRequest(corpusDirectory.Path, "release-1");
        var first = await importer.ImportAsync(firstRequest, CancellationToken.None);
        var repeated = await importer.ImportAsync(firstRequest, CancellationToken.None);

        Assert.True(first.WasCreated);
        Assert.False(repeated.WasCreated);
        Assert.Equal(first.CorpusVersionId, repeated.CorpusVersionId);
        Assert.Equal(first.ImportFingerprint, repeated.ImportFingerprint);
        Assert.Equal(66, first.BookCount);
        Assert.Equal(1, first.VerseCount);
        Assert.Equal(1L, first.WordAnnotationCount);
        Assert.Equal(1L, first.StrongAttributeCount);

        await File.WriteAllTextAsync(
            Path.Combine(corpusDirectory.Path, "GEN.usfm"),
            "second-source");

        reader.Result = CreateParsedCorpus(corpusDirectory.Path);

        var second = await importer.ImportAsync(
            CreateRequest(corpusDirectory.Path, "release-2"),
            CancellationToken.None);

        Assert.True(second.WasCreated);
        Assert.NotEqual(first.CorpusVersionId, second.CorpusVersionId);
        Assert.NotEqual(first.ImportFingerprint, second.ImportFingerprint);

        await context.Database.OpenConnectionAsync();
        Assert.Equal(2L, await ScalarInt64Async(
            context,
            "SELECT COUNT(*) FROM bible_corpus_versions"));
        Assert.Equal(1L, await ScalarInt64Async(
            context,
            "SELECT COUNT(*) FROM bible_corpus_versions WHERE is_active"));
        Assert.Equal(second.CorpusVersionId.Value, await ScalarGuidAsync(
            context,
            "SELECT id FROM bible_corpus_versions WHERE is_active"));
        Assert.Equal(2L, await ScalarInt64Async(
            context,
            "SELECT COUNT(*) FROM bible_verses"));
        Assert.Equal(2L, await ScalarInt64Async(
            context,
            "SELECT COUNT(*) FROM bible_word_annotations"));
        Assert.Equal(2L, await ScalarInt64Async(
            context,
            "SELECT COUNT(*) FROM bible_supplemental_texts"));
    }

    [Fact]
    public async Task Import_ShouldRollbackEveryWriteWhenBulkPersistenceFails()
    {
        await using var context = CreateContext();
        await ResetDatabaseAsync(context);
        using var corpusDirectory = CreateCorpusDirectory("rollback-source");

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE FUNCTION fail_bible_annotation_insert() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'forced annotation persistence failure';
            END;
            $$;
            CREATE TRIGGER fail_bible_annotation_insert
            BEFORE INSERT ON bible_word_annotations
            FOR EACH ROW EXECUTE FUNCTION fail_bible_annotation_insert();
            """);

        var importer = new PostgreSqlBibleCorpusImporter(
            context,
            new StubCorpusReader(CreateParsedCorpus(corpusDirectory.Path)),
            TimeProvider.System);

        try
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                importer.ImportAsync(
                    CreateRequest(corpusDirectory.Path, "broken-release"),
                    CancellationToken.None));
            Assert.Equal("P0001", exception.SqlState);

            await using var verificationContext = CreateContext();
            await verificationContext.Database.OpenConnectionAsync();
            Assert.Equal(0L, await ScalarInt64Async(
                verificationContext,
                "SELECT COUNT(*) FROM bible_editions"));
            Assert.Equal(0L, await ScalarInt64Async(
                verificationContext,
                "SELECT COUNT(*) FROM bible_corpus_versions"));
            Assert.Equal(0L, await ScalarInt64Async(
                verificationContext,
                "SELECT COUNT(*) FROM bible_verses"));
        }
        finally
        {
            await using var cleanupContext = CreateContext();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                """
                DROP TRIGGER IF EXISTS fail_bible_annotation_insert ON bible_word_annotations;
                DROP FUNCTION IF EXISTS fail_bible_annotation_insert();
                """);
        }
    }

    [Fact]
    public async Task Import_ShouldRejectStaleValidationCountsBeforeWriting()
    {
        await using var context = CreateContext();
        await ResetDatabaseAsync(context);
        using var corpusDirectory = CreateCorpusDirectory("stale-evidence-source");

        var importer = new PostgreSqlBibleCorpusImporter(
            context,
            new StubCorpusReader(CreateParsedCorpus(corpusDirectory.Path)),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<BibleCorpusImportException>(() =>
            importer.ImportAsync(
                CreateRequest(
                    corpusDirectory.Path,
                    "stale-release",
                    expectedStrongAttributeCount: 0),
                CancellationToken.None));

        Assert.Contains("Strong attributes 1/0", exception.Message, StringComparison.Ordinal);

        await context.Database.OpenConnectionAsync();
        Assert.Equal(0L, await ScalarInt64Async(
            context,
            "SELECT COUNT(*) FROM bible_editions"));
        Assert.Equal(0L, await ScalarInt64Async(
            context,
            "SELECT COUNT(*) FROM bible_corpus_versions"));
    }

    private static BibleCorpusImportRequest CreateRequest(
        string corpusDirectory,
        string upstreamRevision,
        long expectedStrongAttributeCount = 1)
    {
        var artifactPath = Path.Combine(corpusDirectory, "GEN.usfm");
        var artifactBytes = File.ReadAllBytes(artifactPath);
        var artifactDigest = new Sha256Digest(
            Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant());

        return new BibleCorpusImportRequest(
            new BibleEditionImportDefinition(
                new BibleEditionCode("test-edition"),
                "Test Edition",
                "en",
                "protestant-66"),
            new BibleCorpusReadRequest(corpusDirectory),
            new BibleCorpusValidationEvidence(66, 1, expectedStrongAttributeCount),
            [
                new BibleSourceArtifactImport(
                    BibleSourceArtifactRole.CanonicalUsfm,
                    artifactPath,
                    new Uri("https://example.test/test-edition-usfm.zip"),
                    "test-edition-usfm.zip",
                    artifactDigest,
                    artifactBytes.LongLength,
                    new DateTimeOffset(2026, 8, 2, 18, 0, 0, TimeSpan.Zero))
            ],
            upstreamRevision);
    }

    private static BibleCorpusReadResult CreateParsedCorpus(string corpusDirectory)
    {
        var books = ProtestantBookCodes.Select(
            (code, index) =>
            {
                var bytes = File.ReadAllBytes(Path.Combine(corpusDirectory, $"{code}.usfm"));
                return new ParsedBibleBook(
                    new UsfmBookCode(code),
                    index + 1,
                    code,
                    null,
                    $"{code}.usfm",
                    new Sha256Digest(
                        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()),
                    bytes.LongLength);
            });

        var verse = new ParsedBibleVerse(
            new BibleReference(new UsfmBookCode("GEN"), 1, "1"),
            1,
            "In",
            "GEN.usfm",
            1,
            [new ParsedBibleWordAnnotation(1, "w", "strong", "H7225", 0, 2)],
            [
                new ParsedBibleSupplementalText(
                    1,
                    "d",
                    "A title",
                    BibleSupplementalTextPlacement.Before,
                    null)
            ]);

        return new BibleCorpusReadResult(66, books, [verse]);
    }

    private static TemporaryCorpusDirectory CreateCorpusDirectory(string content)
    {
        var directory = new TemporaryCorpusDirectory();
        foreach (var code in ProtestantBookCodes)
        {
            File.WriteAllText(Path.Combine(directory.Path, $"{code}.usfm"), content);
        }

        return directory;
    }

    private static ApologiaStudioDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_TEST_DB_CONNECTION");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        var options = new DbContextOptionsBuilder<ApologiaStudioDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ApologiaStudioDbContext(options);
    }

    private static async Task ResetDatabaseAsync(ApologiaStudioDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    private static async Task<long> ScalarInt64Async(
        ApologiaStudioDbContext context,
        string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<Guid> ScalarGuidAsync(
        ApologiaStudioDbContext context,
        string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Assert.IsType<Guid>(await command.ExecuteScalarAsync());
    }

    private sealed class StubCorpusReader(BibleCorpusReadResult result) : IBibleCorpusReader
    {
        public BibleCorpusReadResult Result { get; set; } = result;

        public Task<BibleCorpusReadResult> ReadAsync(
            BibleCorpusReadRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TemporaryCorpusDirectory : IDisposable
    {
        public TemporaryCorpusDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"apologia-importer-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
