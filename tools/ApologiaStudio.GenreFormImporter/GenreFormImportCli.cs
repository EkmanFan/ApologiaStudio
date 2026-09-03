using System.IO.Compression;
using System.Security.Cryptography;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.GenreFormImporter;

/// <summary>
/// Administrative synchronizer for the Library of Congress Genre/Form
/// authority. Deliberately a maintenance command: normal Apologia runtime
/// never depends on id.loc.gov availability.
/// </summary>
public static class GenreFormImportCli
{
    private const string Authority = "lcgft";

    private const string DefaultSourceUri =
        "https://id.loc.gov/download/authorities/genreForms.skosrdf.jsonld.gz";

    private const string ImporterVersion = "genre-form-importer/1.0";

    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            WriteUsage();
            return 0;
        }

        var connectionString =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined.");
            return 2;
        }

        var filePath = ReadOption(args, "--file");
        var sourceUri = ReadOption(args, "--source") ?? DefaultSourceUri;
        var applyProfileOnly = args.Contains("--apply-profile");

        try
        {
            if (applyProfileOnly)
            {
                return await ApplyProfileAsync(connectionString, cancellationToken);
            }

            var (payload, sha256) = filePath is null
                ? await DownloadAsync(sourceUri, cancellationToken)
                : await ReadFileAsync(filePath, cancellationToken);

            Console.WriteLine($"source        : {filePath ?? sourceUri}");
            Console.WriteLine($"content sha256: {sha256}");
            Console.WriteLine($"payload bytes : {payload.Length}");

            var reader = new SkosJsonLdGenreFormDatasetReader();

            await using var decompressed = Decompress(payload);
            var dataset = reader.Read(decompressed);

            Console.WriteLine($"representation: {reader.RepresentationId}");
            Console.WriteLine($"terms parsed  : {dataset.Terms.Count}");

            var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
                .UseNpgsql(
                    connectionString,
                    postgres => postgres.UseVector())
                .Options;

            await using var context = new KnowledgeDbContext(options);
            var store = new PostgreSqlGenreFormAuthorityStore(context);

            var snapshot = new GenreFormAuthoritySnapshot(
                Authority,
                filePath is null ? sourceUri : new Uri(Path.GetFullPath(filePath)).ToString(),
                sha256,
                DateTimeOffset.UtcNow,
                ImporterVersion);

            var result = await store.ImportAsync(
                snapshot,
                dataset,
                cancellationToken);

            WriteResult(result);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Import cancelled.");
            return 130;
        }
        catch (GenreFormAuthorityException exception)
        {
            Console.Error.WriteLine($"Authority import failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> ApplyProfileAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;

        await using var context = new KnowledgeDbContext(options);
        var seeder = new PostgreSqlGenreFormProfileSeeder(context);

        var result = await seeder.ApplyAsync(cancellationToken);

        Console.WriteLine($"profile version  : {result.ProfileVersion}");
        Console.WriteLine($"selectable       : {result.SelectableCount}");
        Console.WriteLine($"structural only  : {result.StructuralOnlyCount}");
        Console.WriteLine(
            result.Changed
                ? "profile applied"
                : "profile already current; no change applied");

        foreach (var label in result.StructuralOnlyLabels)
        {
            Console.WriteLine($"  structural: {label}");
        }

        return 0;
    }

    private static void WriteResult(GenreFormAuthorityImportResult result)
    {
        Console.WriteLine();
        Console.WriteLine(
            result.SnapshotAlreadyImported
                ? "snapshot already imported; no semantic change applied"
                : "snapshot imported");
        Console.WriteLine($"snapshot id      : {result.SnapshotId}");
        Console.WriteLine($"terms            : {result.TermCount}");
        Console.WriteLine($"deprecated       : {result.DeprecatedTermCount}");
        Console.WriteLine($"variants         : {result.VariantCount}");
        Console.WriteLine($"notes            : {result.NoteCount}");
        Console.WriteLine($"broader relations: {result.BroaderRelationCount}");
        Console.WriteLine($"related relations: {result.RelatedRelationCount}");

        if (result.ProfileReviewItems.Count == 0)
        {
            Console.WriteLine("profile review   : none required");
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"profile review required for {result.ProfileReviewItems.Count} term(s):");

        foreach (var item in result.ProfileReviewItems)
        {
            Console.WriteLine(
                $"  {item.AuthorityUri}  usage={item.UsageStatus}  " +
                $"assignments={item.WorkAssignmentCount}  \"{item.PreferredLabel}\"");
        }
    }

    private static async Task<(byte[] Payload, string Sha256)> DownloadAsync(
        string sourceUri,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(ImporterVersion);

        var payload = await client.GetByteArrayAsync(sourceUri, cancellationToken);
        return (payload, Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());
    }

    private static async Task<(byte[] Payload, string Sha256)> ReadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var payload = await File.ReadAllBytesAsync(path, cancellationToken);
        return (payload, Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());
    }

    private static Stream Decompress(byte[] payload)
    {
        var buffer = new MemoryStream(payload, writable: false);

        // The published bulk dataset is gzip; a plain file is accepted so a
        // pinned local fixture can be replayed without recompression.
        if (payload.Length >= 2 && payload[0] == 0x1F && payload[1] == 0x8B)
        {
            return new GZipStream(buffer, CompressionMode.Decompress);
        }

        return buffer;
    }

    private static string? ReadOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index < args.Length - 1
            ? args[index + 1]
            : null;
    }

    private static void WriteUsage()
    {
        Console.WriteLine(
            """
            Apologia Genre/Form authority importer

            Usage:
              dotnet run --project tools/ApologiaStudio.GenreFormImporter [options]

            Options:
              --source <uri>   Official bulk dataset URI. Defaults to the
                               Library of Congress LCGFT SKOS/RDF JSON-LD dump.
              --file <path>    Import a already-downloaded dataset instead of
                               fetching it. Accepts .gz or plain JSON Lines.
              --apply-profile  Apply Apologia Genre/Form Profile V1 over the
                               already-imported authority and exit.
              -h, --help       Show this help.

            Environment:
              APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION   Knowledge Store connection.

            The importer is idempotent: re-importing identical content produces
            no semantic change. An authority refresh never alters Apologia
            profile decisions or existing Work assignments; terms that the new
            snapshot no longer publishes are reported for explicit review.
            """);
    }
}
