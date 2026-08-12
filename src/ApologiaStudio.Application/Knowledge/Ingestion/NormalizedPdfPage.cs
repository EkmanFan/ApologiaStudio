namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record NormalizedPdfPage(
    int PageNumber,
    double Width,
    double Height,
    IReadOnlyList<NormalizedPdfTextBlock> Blocks);
