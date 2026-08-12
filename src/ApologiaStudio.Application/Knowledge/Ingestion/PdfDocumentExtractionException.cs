namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed class PdfDocumentExtractionException : Exception
{
    public PdfDocumentExtractionException(string message)
        : base(message)
    {
    }

    public PdfDocumentExtractionException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
