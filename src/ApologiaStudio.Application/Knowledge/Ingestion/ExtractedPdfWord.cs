namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record ExtractedPdfWord(
    int Ordinal,
    int SourceSequence,
    string Text,
    PdfBoundingBox BoundingBox,
    PdfTextOrientation Orientation,
    string? FontName,
    double? MedianPointSize);
