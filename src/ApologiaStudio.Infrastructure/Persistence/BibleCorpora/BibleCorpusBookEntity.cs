using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

internal sealed class BibleCorpusBookEntity
{
    public BibleCorpusVersionId CorpusVersionId { get; set; }

    public UsfmBookCode UsfmBookCode { get; set; }

    public int BookOrdinal { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string SourceRelativePath { get; set; } = string.Empty;
}
