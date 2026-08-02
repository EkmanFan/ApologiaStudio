using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record ParsedBibleBook
{
    public ParsedBibleBook(
        UsfmBookCode bookCode,
        int bookOrdinal,
        string displayName,
        string? shortName,
        string sourceRelativePath,
        Sha256Digest? sourceSha256 = null,
        long? sourceByteLength = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bookOrdinal, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelativePath);

        if ((sourceSha256 is null) != (sourceByteLength is null))
        {
            throw new ArgumentException(
                "Source SHA-256 and byte length must either both be supplied or both be absent.");
        }

        if (sourceByteLength is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceByteLength));
        }

        BookCode = bookCode;
        BookOrdinal = bookOrdinal;
        DisplayName = displayName.Trim();
        ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
        SourceRelativePath = sourceRelativePath.Trim();
        SourceSha256 = sourceSha256;
        SourceByteLength = sourceByteLength;
    }

    public UsfmBookCode BookCode { get; }

    public int BookOrdinal { get; }

    public string DisplayName { get; }

    public string? ShortName { get; }

    public string SourceRelativePath { get; }

    public Sha256Digest? SourceSha256 { get; }

    public long? SourceByteLength { get; }
}
