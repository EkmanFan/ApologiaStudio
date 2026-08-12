namespace ApologiaStudio.Application.Knowledge.Ingestion;

public interface IDocumentSegmenter
{
    DocumentSegmentationResult Segment(
        NormalizedPdfDocument document,
        DocumentSegmentationHints? hints,
        CancellationToken cancellationToken);
}
