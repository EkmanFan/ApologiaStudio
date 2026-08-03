namespace ApologiaStudio.Application.BibleCorpora.Reader;

public sealed record GetBibleReaderQuery(
    string EditionCode,
    string? BookCode = null,
    int? ChapterNumber = null);
