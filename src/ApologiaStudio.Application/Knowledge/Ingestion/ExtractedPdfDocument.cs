namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record ExtractedPdfDocument(
    string SourceFileName,
    string SourceSha256,
    long SourceByteLength,
    string ExtractionProfileId,
    int PageCount,
    IReadOnlyList<ExtractedPdfPage> Pages);
