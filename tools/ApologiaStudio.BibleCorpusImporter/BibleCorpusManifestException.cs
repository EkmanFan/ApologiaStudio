namespace ApologiaStudio.BibleCorpusImporter;

public sealed class BibleCorpusManifestException : Exception
{
    public BibleCorpusManifestException(string message)
        : base(message)
    {
    }

    public BibleCorpusManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
