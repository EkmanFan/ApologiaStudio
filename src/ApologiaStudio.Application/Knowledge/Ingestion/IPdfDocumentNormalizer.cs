namespace ApologiaStudio.Application.Knowledge.Ingestion;

public interface IPdfDocumentNormalizer
{
    NormalizedPdfDocument Normalize(
        ExtractedPdfDocument document,
        CancellationToken cancellationToken);
}
