using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

internal sealed class BibleVerseEntity
{
    public long Id { get; set; }

    public BibleCorpusVersionId CorpusVersionId { get; set; }

    public UsfmBookCode UsfmBookCode { get; set; }

    public int ChapterNumber { get; set; }

    public string VerseLabel { get; set; } = string.Empty;

    public int VerseOrdinal { get; set; }

    public string Text { get; set; } = string.Empty;

    public string SourceRelativePath { get; set; } = string.Empty;

    public int SourceLine { get; set; }
}
