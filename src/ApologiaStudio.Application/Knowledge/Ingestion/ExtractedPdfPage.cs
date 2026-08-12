namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record ExtractedPdfPage(
    int PageNumber,
    double Width,
    double Height,
    IReadOnlyList<ExtractedPdfWord> Words,
    IReadOnlyList<ExtractedPdfTextBlock> Blocks)
{
    public int RasterImageCount { get; init; }

    public double LargestRasterImageAreaRatio { get; init; }
}
