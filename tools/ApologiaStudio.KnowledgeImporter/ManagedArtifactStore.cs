namespace ApologiaStudio.KnowledgeImporter;

internal sealed record MaterializedArtifacts(
    string RawPath,
    string ParsedPath,
    string NormalizedPath,
    IReadOnlyList<string> CreatedPaths);

internal static class ManagedArtifactStore
{
    public static async Task<MaterializedArtifacts> MaterializeAsync(
        PreparedDeDecretis prepared,
        string artifactRoot,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(artifactRoot);
        var rawPath = Path.Combine(root, "raw", prepared.RawSha256 + ".pdf");
        var parsedPath = Path.Combine(
            root,
            "parsed",
            prepared.ParsedArtifact.Sha256 + ".txt");
        var normalizedPath = Path.Combine(
            root,
            "normalized",
            prepared.NormalizedArtifact.Sha256 + ".txt");

        var created = new List<string>();

        try
        {
            if (await EnsureRawCopyAsync(
                    prepared.SourcePath,
                    rawPath,
                    prepared.RawSha256,
                    cancellationToken))
            {
                created.Add(rawPath);
            }

            if (await EnsureBytesAsync(
                    parsedPath,
                    prepared.ParsedArtifact.Bytes,
                    prepared.ParsedArtifact.Sha256,
                    cancellationToken))
            {
                created.Add(parsedPath);
            }

            if (await EnsureBytesAsync(
                    normalizedPath,
                    prepared.NormalizedArtifact.Bytes,
                    prepared.NormalizedArtifact.Sha256,
                    cancellationToken))
            {
                created.Add(normalizedPath);
            }
        }
        catch
        {
            DeleteCreated(created);
            throw;
        }

        return new MaterializedArtifacts(
            rawPath,
            parsedPath,
            normalizedPath,
            created);
    }

    public static void DeleteCreated(IEnumerable<string> paths)
    {
        foreach (var path in paths.Reverse())
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
        PreparedDeDecretis prepared,
        string artifactRoot,
        IReadOnlySet<string> deletableHashes)
    {
        var root = Path.GetFullPath(artifactRoot);
        var candidates = new[]
        {
            (prepared.RawSha256, Path.Combine(root, "raw", prepared.RawSha256 + ".pdf")),
            (prepared.ParsedArtifact.Sha256, Path.Combine(root, "parsed", prepared.ParsedArtifact.Sha256 + ".txt")),
            (prepared.NormalizedArtifact.Sha256, Path.Combine(root, "normalized", prepared.NormalizedArtifact.Sha256 + ".txt"))
        };

        foreach (var (hash, path) in candidates)
        {
            if (deletableHashes.Contains(hash) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static async Task<bool> EnsureRawCopyAsync(
        string sourcePath,
        string destinationPath,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (File.Exists(destinationPath))
        {
            EnsureFileHash(destinationPath, expectedSha256);
            return false;
        }

        var tempPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var input = new FileStream(
                             sourcePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            EnsureFileHash(tempPath, expectedSha256);
            try
            {
                File.Move(tempPath, destinationPath);
                return true;
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                EnsureFileHash(destinationPath, expectedSha256);
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
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (File.Exists(destinationPath))
        {
            EnsureFileHash(destinationPath, expectedSha256);
            return false;
        }

        var tempPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
            EnsureFileHash(tempPath, expectedSha256);
            try
            {
                File.Move(tempPath, destinationPath);
                return true;
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                EnsureFileHash(destinationPath, expectedSha256);
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

    private static void EnsureFileHash(string path, string expectedSha256)
    {
        using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var actual = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();

        if (!string.Equals(actual, expectedSha256, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Managed artifact hash mismatch for {path}. " +
                $"Expected {expectedSha256}, found {actual}.");
        }
    }
}
