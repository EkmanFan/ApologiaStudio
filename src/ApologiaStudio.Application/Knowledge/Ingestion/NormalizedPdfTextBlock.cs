namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record NormalizedPdfTextBlock(
    int ReadingOrder,
    string SourceText,
    string Text,
    PdfBoundingBox BoundingBox,
    PdfTextOrientation Orientation,
    string? DominantFontName,
    double? MedianPointSize,
    int LineCount,
    int WordCount,
    int? FirstSourceSequence,
    int? LastSourceSequence,
    bool IsExcluded,
    PdfBlockExclusionReason? ExclusionReason);
