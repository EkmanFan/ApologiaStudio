namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed class BibleCorpusReadException : Exception
{
    public BibleCorpusReadException(string message)
        : base(message)
    {
    }

    public BibleCorpusReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
