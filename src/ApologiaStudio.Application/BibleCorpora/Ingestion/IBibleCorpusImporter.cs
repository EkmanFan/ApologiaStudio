namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public interface IBibleCorpusImporter
{
    Task<BibleCorpusImportResult> ImportAsync(
        BibleCorpusImportRequest request,
        CancellationToken cancellationToken);
}
