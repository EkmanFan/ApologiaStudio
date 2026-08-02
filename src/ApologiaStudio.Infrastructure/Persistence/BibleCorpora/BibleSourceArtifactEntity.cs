using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

internal sealed class BibleSourceArtifactEntity
{
    public long Id { get; set; }

    public BibleCorpusVersionId CorpusVersionId { get; set; }

    public string Role { get; set; } = string.Empty;

    public string SourceUri { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public Sha256Digest Sha256 { get; set; }

    public long ByteLength { get; set; }

    public DateTimeOffset DownloadedAt { get; set; }
}
