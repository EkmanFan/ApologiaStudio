using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record ParsedBibleBook
{
    public ParsedBibleBook(
        UsfmBookCode bookCode,
        int bookOrdinal,
        string displayName,
        string? shortName,
        string sourceRelativePath)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bookOrdinal, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRelativePath);

        BookCode = bookCode;
        BookOrdinal = bookOrdinal;
        DisplayName = displayName.Trim();
        ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
        SourceRelativePath = sourceRelativePath.Trim();
    }

    public UsfmBookCode BookCode { get; }

    public int BookOrdinal { get; }

    public string DisplayName { get; }

    public string? ShortName { get; }

    public string SourceRelativePath { get; }
}
