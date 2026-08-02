using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;
using ApologiaStudio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ApologiaStudio.BibleCorpusImporter;

public static class ImportCli
{
    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            WriteUsage();
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            var options = ImportOptions.Parse(args);
            var manifest = await BibleCorpusManifestLoader.LoadAsync(
                options.ManifestPath,
                cancellationToken);
            if (!string.Equals(
                    manifest.ManifestId,
                    options.ConfirmedManifestId,
                    StringComparison.Ordinal))
            {
                throw new BibleCorpusManifestException(
                    $"Confirmation '{options.ConfirmedManifestId}' does not match manifest "
                    + $"'{manifest.ManifestId}'.");
            }

            var connectionString = Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_DB_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new BibleCorpusManifestException(
                    "APOLOGIASTUDIO_DB_CONNECTION must be defined.");
            }

            using var preparation = await ManifestImportPreparation.CreateAsync(
                manifest,
                options.ArtifactsDirectory,
                cancellationToken);

            var dbOptions = new DbContextOptionsBuilder<ApologiaStudioDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var dbContext = new ApologiaStudioDbContext(dbOptions);

            var pendingMigrations = (await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken))
                .ToArray();
            if (pendingMigrations.Length > 0)
            {
                throw new BibleCorpusManifestException(
                    "Database schema is not current. Apply migrations before importing: "
                    + string.Join(", ", pendingMigrations));
            }

            var target = new NpgsqlConnectionStringBuilder(connectionString);
            Console.WriteLine(
                $"Importing {manifest.ManifestId} into "
                + $"{target.Database} on {target.Host}:{target.Port} as {target.Username}...");

            var importer = new PostgreSqlBibleCorpusImporter(
                dbContext,
                new SilMachineUsfmCorpusReader(),
                TimeProvider.System);
            var result = await importer.ImportAsync(
                preparation.Request,
                cancellationToken);

            Console.WriteLine(result.WasCreated ? "RESULT: IMPORTED" : "RESULT: ALREADY IMPORTED");
            Console.WriteLine($"Manifest: {manifest.ManifestId}");
            Console.WriteLine($"Corpus version: {result.CorpusVersionId}");
            Console.WriteLine($"Import fingerprint: {result.ImportFingerprint}");
            Console.WriteLine($"Books: {result.BookCount}");
            Console.WriteLine($"Verses: {result.VerseCount}");
            Console.WriteLine($"Word annotations: {result.WordAnnotationCount}");
            Console.WriteLine($"Strong attributes: {result.StrongAttributeCount}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Bible corpus import was cancelled.");
            return 130;
        }
        catch (Exception exception) when (
            exception is BibleCorpusManifestException
                or BibleCorpusImportException
                or BibleCorpusReadException
                or NpgsqlException
                or InvalidOperationException)
        {
            Console.Error.WriteLine($"Bible corpus import failed: {exception.Message}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected failure: {exception}");
            return 2;
        }
    }

    private static void WriteUsage()
    {
        Console.WriteLine(
            """
            Import one approved Bible corpus manifest into PostgreSQL.

            The command verifies both source archives, safely extracts the canonical
            USFM archive, parses it with the production reader, and performs one
            transactional, idempotent import. It never downloads sources or applies
            database migrations.

            Required environment variable:
              APOLOGIASTUDIO_DB_CONNECTION   PostgreSQL connection string.

            Usage:
              dotnet run --project tools/ApologiaStudio.BibleCorpusImporter -- \
                --manifest corpora/manifests/fraLSG-2026-08-02.json \
                --artifacts /absolute/path/to/downloaded/archives \
                --confirm-manifest fraLSG-2026-08-02

            Options:
              --manifest <path>              Approved JSON manifest.
              --artifacts <directory>        Directory containing manifest-named ZIP files.
              --confirm-manifest <id>        Must exactly match manifestId.
              --help                         Show this help.
            """);
    }

    private sealed record ImportOptions(
        string ManifestPath,
        string ArtifactsDirectory,
        string ConfirmedManifestId)
    {
        public static ImportOptions Parse(IReadOnlyList<string> args)
        {
            string? manifestPath = null;
            string? artifactsDirectory = null;
            string? confirmedManifestId = null;

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--manifest":
                        manifestPath = ReadValue(args, ref index, "--manifest");
                        break;
                    case "--artifacts":
                        artifactsDirectory = ReadValue(args, ref index, "--artifacts");
                        break;
                    case "--confirm-manifest":
                        confirmedManifestId = ReadValue(args, ref index, "--confirm-manifest");
                        break;
                    default:
                        throw new BibleCorpusManifestException($"Unknown option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new BibleCorpusManifestException("Missing required option --manifest.");
            }

            if (string.IsNullOrWhiteSpace(artifactsDirectory))
            {
                throw new BibleCorpusManifestException("Missing required option --artifacts.");
            }

            if (string.IsNullOrWhiteSpace(confirmedManifestId))
            {
                throw new BibleCorpusManifestException("Missing required option --confirm-manifest.");
            }

            return new ImportOptions(
                Path.GetFullPath(manifestPath),
                Path.GetFullPath(artifactsDirectory),
                confirmedManifestId);
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string option)
        {
            index++;
            if (index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new BibleCorpusManifestException($"Missing value for {option}.");
            }

            return args[index];
        }
    }
}
