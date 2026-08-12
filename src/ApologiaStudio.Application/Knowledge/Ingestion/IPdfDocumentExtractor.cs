namespace ApologiaStudio.Application.Knowledge.Ingestion;

public interface IPdfDocumentExtractor
{
    Task<ExtractedPdfDocument> ExtractAsync(
        string sourcePath,
        CancellationToken cancellationToken);
}
