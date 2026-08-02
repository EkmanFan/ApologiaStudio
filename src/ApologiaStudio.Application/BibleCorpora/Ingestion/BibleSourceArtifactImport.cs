using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record BibleSourceArtifactImport
{
    public BibleSourceArtifactImport(
        BibleSourceArtifactRole role,
        string localPath,
        Uri sourceUri,
        string fileName,
        Sha256Digest expectedSha256,
        long expectedByteLength,
        DateTimeOffset downloadedAt)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        ArgumentNullException.ThrowIfNull(sourceUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedByteLength, 1);

        if (!sourceUri.IsAbsoluteUri || sourceUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Artifact source URI must be an absolute HTTPS URI.", nameof(sourceUri));
        }

        if (sourceUri.AbsoluteUri.Length > 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceUri));
        }

        if (fileName.Trim().Length > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(fileName));
        }

        Role = role;
        LocalPath = localPath.Trim();
        SourceUri = sourceUri;
        FileName = fileName.Trim();
        ExpectedSha256 = expectedSha256;
        ExpectedByteLength = expectedByteLength;
        DownloadedAt = downloadedAt.ToUniversalTime();
    }

    public BibleSourceArtifactRole Role { get; }

    public string LocalPath { get; }

    public Uri SourceUri { get; }

    public string FileName { get; }

    public Sha256Digest ExpectedSha256 { get; }

    public long ExpectedByteLength { get; }

    public DateTimeOffset DownloadedAt { get; }
}

public enum BibleSourceArtifactRole
{
    CanonicalUsfm = 1,
    ValidationVpl = 2,
    ValidationReport = 3
}
