using System.Security.Cryptography;
using System.Text;
using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;

internal static class BibleCorpusImportHashing
{
    private static readonly byte[] Separator = [0];
    private static readonly byte[] LineFeed = [(byte)'\n'];

    public static async Task VerifyArtifactsAsync(
        IReadOnlyList<BibleSourceArtifactImport> artifacts,
        CancellationToken cancellationToken)
    {
        foreach (var artifact in artifacts)
        {
            var path = Path.GetFullPath(artifact.LocalPath);
            if (!File.Exists(path))
            {
                throw new BibleCorpusImportException($"Source artifact does not exist: {path}");
            }

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length != artifact.ExpectedByteLength)
            {
                throw new BibleCorpusImportException(
                    $"Source artifact length mismatch for {artifact.FileName}: "
                    + $"expected {artifact.ExpectedByteLength}, found {fileInfo.Length}.");
            }

            var digest = await ComputeFileDigestAsync(path, cancellationToken);
            if (digest != artifact.ExpectedSha256)
            {
                throw new BibleCorpusImportException(
                    $"Source artifact SHA-256 mismatch for {artifact.FileName}: "
                    + $"expected {artifact.ExpectedSha256}, found {digest}.");
            }
        }
    }

    public static Sha256Digest ComputeSourceTreeDigest(
        IReadOnlyList<ParsedBibleBook> books)
    {
        using var treeHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var book in books.OrderBy(book => book.SourceRelativePath, StringComparer.Ordinal))
        {
            var relativePath = book.SourceRelativePath.Replace('\\', '/');
            if (book.SourceSha256 is not { } fileDigest || book.SourceByteLength is not > 0)
            {
                throw new BibleCorpusImportException(
                    $"Parsed source {relativePath} does not carry its byte-level integrity metadata.");
            }

            treeHash.AppendData(Encoding.UTF8.GetBytes(relativePath));
            treeHash.AppendData(Separator);
            treeHash.AppendData(Encoding.ASCII.GetBytes(fileDigest.Value));
            treeHash.AppendData(LineFeed);
        }

        return new Sha256Digest(Convert.ToHexString(treeHash.GetHashAndReset()).ToLowerInvariant());
    }

    public static Sha256Digest ComputeImportFingerprint(
        BibleEditionCode editionCode,
        Sha256Digest sourceTreeDigest,
        string parserName,
        string parserVersion,
        string normalizationPolicyId,
        int canonicalSchemaVersion)
    {
        var canonicalInput = string.Join(
            '\n',
            "apologia-bible-import-fingerprint-v1",
            $"edition={editionCode.Value}",
            $"source-tree-sha256={sourceTreeDigest.Value}",
            $"parser={parserName}",
            $"parser-version={parserVersion}",
            $"normalization={normalizationPolicyId}",
            $"canonical-schema={canonicalSchemaVersion}",
            string.Empty);

        return new Sha256Digest(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalInput)))
                .ToLowerInvariant());
    }

    private static async Task<Sha256Digest> ComputeFileDigestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var digest = await SHA256.HashDataAsync(stream, cancellationToken);
        return new Sha256Digest(Convert.ToHexString(digest).ToLowerInvariant());
    }
}
