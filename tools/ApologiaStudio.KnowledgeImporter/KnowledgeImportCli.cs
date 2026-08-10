using Npgsql;

namespace ApologiaStudio.KnowledgeImporter;

public static class KnowledgeImportCli
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
            var prepared = DeDecretisDocument.Prepare(
                options.SourcePath,
                cancellationToken);

            WritePreparedSummary(prepared);

            if (options.Command == ImportCommand.Validate)
            {
                Console.WriteLine("RESULT: VALID");
                return 0;
            }

            var connectionString = Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new KnowledgeImportException(
                    "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION must be defined for import or remove.");
            }

            if (options.Command == ImportCommand.Remove)
            {
                var deletableHashes = await KnowledgeStoreWriter.RemoveAsync(
                    connectionString,
                    prepared,
                    cancellationToken);

                if (options.DeleteArtifacts)
                {
                    ManagedArtifactStore.DeleteArtifacts(
                        prepared,
                        options.ArtifactRoot,
                        deletableHashes);
                }

                Console.WriteLine("RESULT: REMOVED");
                return 0;
            }

            var materialized = await ManagedArtifactStore.MaterializeAsync(
                prepared,
                options.ArtifactRoot,
                cancellationToken);

            try
            {
                var result = await KnowledgeStoreWriter.ImportAsync(
                    connectionString,
                    prepared,
                    cancellationToken);

                Console.WriteLine(result.WasCreated ? "RESULT: IMPORTED" : "RESULT: ALREADY IMPORTED");
                Console.WriteLine($"Work: {result.WorkId}");
                Console.WriteLine($"Normalized artifact: {result.NormalizedArtifactId}");
                Console.WriteLine($"Segments: {result.SegmentCount}");
                Console.WriteLine($"Managed raw: {materialized.RawPath}");
                Console.WriteLine($"Managed parsed: {materialized.ParsedPath}");
                Console.WriteLine($"Managed normalized: {materialized.NormalizedPath}");
                return 0;
            }
            catch
            {
                ManagedArtifactStore.DeleteCreated(materialized.CreatedPaths);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Knowledge import was cancelled.");
            return 130;
        }
        catch (Exception exception) when (
            exception is KnowledgeImportException
                or NpgsqlException
                or IOException
                or UnauthorizedAccessException
                or InvalidOperationException)
        {
            Console.Error.WriteLine($"Knowledge import failed: {exception.Message}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected failure: {exception}");
            return 2;
        }
    }

    private static void WritePreparedSummary(PreparedDeDecretis prepared)
    {
        Console.WriteLine($"Profile: {DeDecretisDocument.ProfileId}");
        Console.WriteLine($"Source: {prepared.SourcePath}");
        Console.WriteLine($"Source SHA-256: {prepared.RawSha256}");
        Console.WriteLine($"Source bytes: {prepared.RawByteLength}");
        Console.WriteLine($"PDF pages selected: {DeDecretisDocument.FirstPdfPage}-{DeDecretisDocument.LastPdfPage}");
        Console.WriteLine($"Printed pages: {DeDecretisDocument.FirstPdfPage - DeDecretisDocument.PdfToPrintedPageOffset}-{DeDecretisDocument.LastPdfPage - DeDecretisDocument.PdfToPrintedPageOffset}");
        Console.WriteLine($"Parsed SHA-256: {prepared.ParsedArtifact.Sha256}");
        Console.WriteLine($"Normalized SHA-256: {prepared.NormalizedArtifact.Sha256}");
        Console.WriteLine($"Sections: {prepared.Segments.Count}");
    }

    private static void WriteUsage()
    {
        Console.WriteLine(
            """
            Validate, import, or remove the curated De Decretis source profile.

            The importer never downloads source material and never modifies the source PDF.
            It accepts only the reviewed NPNF2-04 PDF with the pinned SHA-256.

            Required for import/remove:
              APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION   Knowledge PostgreSQL connection string.

            Usage:
              dotnet run --project tools/ApologiaStudio.KnowledgeImporter -- \
                validate --source /absolute/path/to/npnf204.pdf

              dotnet run --project tools/ApologiaStudio.KnowledgeImporter -- \
                import --source /absolute/path/to/npnf204.pdf \
                --artifact-root /absolute/path/to/managed/artifacts

              dotnet run --project tools/ApologiaStudio.KnowledgeImporter -- \
                remove --source /absolute/path/to/npnf204.pdf \
                --artifact-root /absolute/path/to/managed/artifacts \
                --delete-artifacts
            """);
    }

    private enum ImportCommand
    {
        Validate,
        Import,
        Remove
    }

    private sealed record ImportOptions(
        ImportCommand Command,
        string SourcePath,
        string ArtifactRoot,
        bool DeleteArtifacts)
    {
        public static ImportOptions Parse(IReadOnlyList<string> args)
        {
            var command = args[0] switch
            {
                "validate" => ImportCommand.Validate,
                "import" => ImportCommand.Import,
                "remove" => ImportCommand.Remove,
                _ => throw new KnowledgeImportException(
                    $"Unknown command '{args[0]}'. Expected validate, import, or remove.")
            };

            string? sourcePath = null;
            string? artifactRoot = null;
            var deleteArtifacts = false;

            for (var index = 1; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--source":
                        sourcePath = ReadValue(args, ref index, "--source");
                        break;
                    case "--artifact-root":
                        artifactRoot = ReadValue(args, ref index, "--artifact-root");
                        break;
                    case "--delete-artifacts":
                        deleteArtifacts = true;
                        break;
                    default:
                        throw new KnowledgeImportException(
                            $"Unknown option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new KnowledgeImportException("Missing required option --source.");
            }

            if (command != ImportCommand.Validate && string.IsNullOrWhiteSpace(artifactRoot))
            {
                throw new KnowledgeImportException(
                    "Missing required option --artifact-root for import/remove.");
            }

            if (deleteArtifacts && command != ImportCommand.Remove)
            {
                throw new KnowledgeImportException(
                    "--delete-artifacts is valid only with the remove command.");
            }

            return new ImportOptions(
                command,
                Path.GetFullPath(sourcePath),
                string.IsNullOrWhiteSpace(artifactRoot)
                    ? Path.GetFullPath(".")
                    : Path.GetFullPath(artifactRoot),
                deleteArtifacts);
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string option)
        {
            index++;
            if (index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new KnowledgeImportException($"Missing value for {option}.");
            }

            return args[index];
        }
    }
}

internal sealed class KnowledgeImportException(string message)
    : Exception(message);
