namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record DocumentSegmentationResult(
    string SourceFileName,
    string SourceSha256,
    long SourceByteLength,
    string ExtractionProfileId,
    string NormalizationProfileId,
    string SegmentationProfileId,
    IReadOnlyList<DocumentSegmentDraft> Segments);
