namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record DocumentSegmentDraft(
    int Ordinal,
    DocumentSegmentType Type,
    DocumentSegmentKind Kind,
    string? Title,
    string Text,
    int StartPage,
    int EndPage,
    IReadOnlyList<DocumentBlockReference> SourceBlocks);
