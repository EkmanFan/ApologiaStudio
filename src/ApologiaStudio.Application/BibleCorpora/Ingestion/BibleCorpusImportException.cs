namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed class BibleCorpusImportException : Exception
{
    public BibleCorpusImportException(string message)
        : base(message)
    {
    }

    public BibleCorpusImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
