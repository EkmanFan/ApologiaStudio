namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record ExtractedPdfTextBlock(
    int ReadingOrder,
    string Text,
    PdfBoundingBox BoundingBox,
    PdfTextOrientation Orientation,
    string? DominantFontName,
    double? MedianPointSize,
    int LineCount,
    int WordCount,
    int? FirstSourceSequence,
    int? LastSourceSequence);
