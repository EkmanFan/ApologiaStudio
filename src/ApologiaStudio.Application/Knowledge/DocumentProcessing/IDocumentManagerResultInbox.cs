namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public interface IDocumentManagerResultInbox
{
    Task<DocumentManagerInboxWriteStatus> StoreAsync(
        ReceivedDocumentManagerResult result,
        CancellationToken cancellationToken);
}
