namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record NormalizedPdfDocument(
    string SourceFileName,
    string SourceSha256,
    long SourceByteLength,
    string ExtractionProfileId,
    string NormalizationProfileId,
    int PageCount,
    IReadOnlyList<NormalizedPdfPage> Pages);
