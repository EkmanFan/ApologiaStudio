namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public interface IDocumentManagerEditorialDraftStore
{
    Task<DocumentManagerEditorialDraftWriteResult> StoreAsync(
        DocumentManagerEditorialDraft draft,
        CancellationToken cancellationToken);
}
