namespace ApologiaStudio.Web.Components.BibleReader;

public sealed record BibleReaderSelection(
    string EditionCode,
    string BookCode,
    int ChapterNumber,
    string StartVerseLabel,
    string EndVerseLabel);
