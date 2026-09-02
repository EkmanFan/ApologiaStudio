namespace ApologiaStudio.Web.DocumentManager;

public interface IDocumentManagerDeliveryReplayClient
{
    Task ReplaySubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken);
}
