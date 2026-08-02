namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public interface IBibleCorpusReader
{
    Task<BibleCorpusReadResult> ReadAsync(
        BibleCorpusReadRequest request,
        CancellationToken cancellationToken);
}
