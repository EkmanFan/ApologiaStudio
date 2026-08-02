using System.IO.Compression;
using System.Security.Cryptography;
using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.BibleCorpusImporter;

public sealed class ManifestImportPreparation : IDisposable
{
    private const int MaximumArchiveEntries = 256;
    private const long MaximumEntryBytes = 32L * 1024 * 1024;
    private const long MaximumExtractedBytes = 256L * 1024 * 1024;
    private static readonly HashSet<string> UsfmExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".sfm", ".usfm" };

    private readonly string temporaryDirectory;
    private bool disposed;

    private ManifestImportPreparation(
        BibleCorpusImportRequest request,
        string temporaryDirectory)
    {
        Request = request;
        this.temporaryDirectory = temporaryDirectory;
    }

    public BibleCorpusImportRequest Request { get; }

    public static async Task<ManifestImportPreparation> CreateAsync(
        BibleCorpusManifest manifest,
        string artifactsDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsDirectory);

        var artifactRoot = Path.GetFullPath(artifactsDirectory);
        if (!Directory.Exists(artifactRoot))
        {
            throw new BibleCorpusManifestException(
                $"Artifact directory does not exist: {artifactRoot}");
        }

        var artifacts = manifest.Source.Artifacts
            .Select(artifact => CreateArtifact(manifest, artifactRoot, artifact))
            .ToArray();
        await VerifyArtifactsAsync(artifacts, cancellationToken);

        var canonicalArtifact = artifacts.Single(
            artifact => artifact.Role == BibleSourceArtifactRole.CanonicalUsfm);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"apologia-corpus-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            await ExtractUsfmAsync(
                canonicalArtifact.LocalPath,
                temporaryDirectory,
                cancellationToken);

            var request = new BibleCorpusImportRequest(
                new BibleEditionImportDefinition(
                    new BibleEditionCode(manifest.Edition.Code),
                    manifest.Edition.DisplayName,
                    manifest.Edition.LanguageTag,
                    manifest.Edition.CanonCode),
                new BibleCorpusReadRequest(
                    temporaryDirectory,
                    manifest.Selection.ExcludedUsfmIds.Select(code => new UsfmBookCode(code))),
                new BibleCorpusValidationEvidence(
                    manifest.Validation.UsfmBookCount,
                    manifest.Validation.UsfmVerseCount,
                    manifest.Validation.StrongAttributeCount),
                artifacts,
                manifest.ManifestId);

            return new ManifestImportPreparation(request, temporaryDirectory);
        }
        catch
        {
            Directory.Delete(temporaryDirectory, recursive: true);
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static BibleSourceArtifactImport CreateArtifact(
        BibleCorpusManifest manifest,
        string artifactRoot,
        ManifestArtifact artifact)
    {
        var role = artifact.Role switch
        {
            "canonical-usfm" => BibleSourceArtifactRole.CanonicalUsfm,
            "validation-vpl" => BibleSourceArtifactRole.ValidationVpl,
            _ => throw new BibleCorpusManifestException(
                $"Unsupported source artifact role: {artifact.Role}")
        };

        return new BibleSourceArtifactImport(
            role,
            Path.Combine(artifactRoot, artifact.FileName),
            new Uri(artifact.Uri, UriKind.Absolute),
            artifact.FileName,
            new Sha256Digest(artifact.Sha256),
            artifact.ByteLength,
            manifest.Source.CapturedAt);
    }

    private static async Task VerifyArtifactsAsync(
        IReadOnlyList<BibleSourceArtifactImport> artifacts,
        CancellationToken cancellationToken)
    {
        foreach (var artifact in artifacts)
        {
            var path = Path.GetFullPath(artifact.LocalPath);
            if (!File.Exists(path))
            {
                throw new BibleCorpusManifestException(
                    $"Manifest artifact does not exist: {path}");
            }

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length != artifact.ExpectedByteLength)
            {
                throw new BibleCorpusManifestException(
                    $"Artifact length mismatch for {artifact.FileName}: "
                    + $"expected {artifact.ExpectedByteLength}, found {fileInfo.Length}.");
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            var digest = new Sha256Digest(Convert.ToHexString(hash).ToLowerInvariant());
            if (digest != artifact.ExpectedSha256)
            {
                throw new BibleCorpusManifestException(
                    $"Artifact SHA-256 mismatch for {artifact.FileName}: "
                    + $"expected {artifact.ExpectedSha256}, found {digest}.");
            }
        }
    }

    private static async Task ExtractUsfmAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var archiveStream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);

            if (archive.Entries.Count > MaximumArchiveEntries)
            {
                throw new BibleCorpusManifestException(
                    $"USFM archive contains {archive.Entries.Count} entries; maximum is {MaximumArchiveEntries}.");
            }

            var entries = archive.Entries
                .Where(entry => UsfmExtensions.Contains(Path.GetExtension(entry.FullName)))
                .ToArray();
            if (entries.Length == 0)
            {
                throw new BibleCorpusManifestException("Canonical archive contains no USFM files.");
            }

            long totalLength = 0;
            foreach (var entry in entries)
            {
                if (entry.Length <= 0 || entry.Length > MaximumEntryBytes)
                {
                    throw new BibleCorpusManifestException(
                        $"Unsafe uncompressed size for archive entry {entry.FullName}: {entry.Length} bytes.");
                }

                checked
                {
                    totalLength += entry.Length;
                }
            }

            if (totalLength > MaximumExtractedBytes)
            {
                throw new BibleCorpusManifestException(
                    $"USFM archive expands to {totalLength} bytes; maximum is {MaximumExtractedBytes}.");
            }

            var destinationRoot = Path.GetFullPath(destinationDirectory)
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                var targetPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
                if (!targetPath.StartsWith(destinationRoot, pathComparison))
                {
                    throw new BibleCorpusManifestException(
                        $"Archive entry escapes the extraction directory: {entry.FullName}");
                }

                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                await using var input = entry.Open();
                await using var output = new FileStream(
                    targetPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 128 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await input.CopyToAsync(output, cancellationToken);
                if (output.Length != entry.Length)
                {
                    throw new BibleCorpusManifestException(
                        $"Archive entry length changed during extraction: {entry.FullName}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BibleCorpusManifestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or OverflowException)
        {
            throw new BibleCorpusManifestException(
                $"Unable to extract canonical USFM archive {archivePath}: {exception.Message}",
                exception);
        }
    }
}
