using ApologiaStudio.Application.Knowledge.Ingestion;

namespace ApologiaStudio.Infrastructure.Knowledge.Ingestion;

public sealed record MaterializedKnowledgeArtifacts(
    IReadOnlyDictionary<Guid, string> PathsByArtifactId,
    IReadOnlyList<string> CreatedPaths)
{
    public string GetRequiredPath(Guid artifactId) =>
        PathsByArtifactId.TryGetValue(
            artifactId,
            out var path)
            ? path
            : throw new InvalidOperationException(
                $"Managed artifact path was not materialized for {artifactId}.");
}

public static class ManagedKnowledgeArtifactStore
{
    public static async Task<MaterializedKnowledgeArtifacts> MaterializeAsync(
        KnowledgeImportPackage package,
        string artifactRoot,
        CancellationToken cancellationToken)
    {
        KnowledgeImportPackageValidator.Validate(package);

        var root = Path.GetFullPath(artifactRoot);
        var paths = new Dictionary<Guid, string>();
        var created = new List<string>();

        try
        {
            foreach (var artifact in package.Artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var path = GetManagedPath(
                    root,
                    artifact);

                var wasCreated =
                    artifact.SourcePath is { } sourcePath
                        ? await EnsureSourceCopyAsync(
                            sourcePath,
                            path,
                            artifact.Sha256,
                            artifact.ByteLength,
                            cancellationToken)
                        : await EnsureBytesAsync(
                            path,
                            artifact.Bytes!,
                            artifact.Sha256,
                            artifact.ByteLength,
                            cancellationToken);

                paths.Add(
                    artifact.Id,
                    path);

                if (wasCreated)
                {
                    created.Add(path);
                }
            }
        }
        catch
        {
            DeleteCreated(created);
            throw;
        }

        return new MaterializedKnowledgeArtifacts(
            paths,
            created);
    }

    public static void DeleteCreated(
        IEnumerable<string> paths)
    {
        foreach (var path in paths
                     .Distinct(StringComparer.Ordinal)
                     .Reverse())
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Preserve the original failure. Cleanup is best-effort.
            }
        }
    }

    public static void DeleteArtifacts(
        KnowledgeImportPackage package,
        string artifactRoot,
        IReadOnlySet<string> deletableHashes)
    {
        KnowledgeImportPackageValidator.Validate(package);

        var root = Path.GetFullPath(artifactRoot);

        foreach (var artifact in package.Artifacts)
        {
            if (!deletableHashes.Contains(artifact.Sha256))
            {
                continue;
            }

            var path = GetManagedPath(
                root,
                artifact);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string GetManagedPath(
        string root,
        KnowledgeImportArtifact artifact) =>
        Path.Combine(
            root,
            artifact.ArtifactType,
            artifact.Sha256 + artifact.FileExtension);

    private static async Task<bool> EnsureSourceCopyAsync(
        string sourcePath,
        string destinationPath,
        string expectedSha256,
        long expectedByteLength,
        CancellationToken cancellationToken)
    {
        var fullSourcePath =
            Path.GetFullPath(sourcePath);

        if (!File.Exists(fullSourcePath))
        {
            throw new InvalidOperationException(
                $"Source artifact was not found: {fullSourcePath}");
        }

        var sourceInfo =
            new FileInfo(fullSourcePath);

        if (sourceInfo.Length != expectedByteLength)
        {
            throw new InvalidOperationException(
                $"Source artifact byte length mismatch for {fullSourcePath}. " +
                $"Expected {expectedByteLength}, found {sourceInfo.Length}.");
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(destinationPath)!);

        if (File.Exists(destinationPath))
        {
            EnsureFileIdentity(
                destinationPath,
                expectedSha256,
                expectedByteLength);
            return false;
        }

        var tempPath =
            destinationPath +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        try
        {
            await using (var input = new FileStream(
                             fullSourcePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             128 * 1024,
                             FileOptions.Asynchronous |
                             FileOptions.SequentialScan))
            await using (var output = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous |
                             FileOptions.SequentialScan))
            {
                await input.CopyToAsync(
                    output,
                    cancellationToken);
                await output.FlushAsync(
                    cancellationToken);
            }

            EnsureFileIdentity(
                tempPath,
                expectedSha256,
                expectedByteLength);

            try
            {
                File.Move(
                    tempPath,
                    destinationPath);
                return true;
            }
            catch (IOException)
                when (File.Exists(destinationPath))
            {
                EnsureFileIdentity(
                    destinationPath,
                    expectedSha256,
                    expectedByteLength);
                return false;
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async Task<bool> EnsureBytesAsync(
        string destinationPath,
        byte[] bytes,
        string expectedSha256,
        long expectedByteLength,
        CancellationToken cancellationToken)
    {
        if (bytes.LongLength != expectedByteLength)
        {
            throw new InvalidOperationException(
                $"Prepared artifact byte length mismatch for {destinationPath}.");
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(destinationPath)!);

        if (File.Exists(destinationPath))
        {
            EnsureFileIdentity(
                destinationPath,
                expectedSha256,
                expectedByteLength);
            return false;
        }

        var tempPath =
            destinationPath +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        try
        {
            await File.WriteAllBytesAsync(
                tempPath,
                bytes,
                cancellationToken);

            EnsureFileIdentity(
                tempPath,
                expectedSha256,
                expectedByteLength);

            try
            {
                File.Move(
                    tempPath,
                    destinationPath);
                return true;
            }
            catch (IOException)
                when (File.Exists(destinationPath))
            {
                EnsureFileIdentity(
                    destinationPath,
                    expectedSha256,
                    expectedByteLength);
                return false;
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void EnsureFileIdentity(
        string path,
        string expectedSha256,
        long expectedByteLength)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length != expectedByteLength)
        {
            throw new InvalidOperationException(
                $"Managed artifact byte length mismatch for {path}. " +
                $"Expected {expectedByteLength}, found {fileInfo.Length}.");
        }

        using var stream = File.OpenRead(path);
        using var sha =
            System.Security.Cryptography.SHA256.Create();

        var actual = Convert
            .ToHexString(sha.ComputeHash(stream))
            .ToLowerInvariant();

        if (!string.Equals(
                actual,
                expectedSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Managed artifact hash mismatch for {path}. " +
                $"Expected {expectedSha256}, found {actual}.");
        }
    }
}
