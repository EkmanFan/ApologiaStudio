namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public sealed record PurgeDocumentManagerSubmissionCommand(
    Guid DraftId,
    int ExpectedVersion);

public sealed record PurgedDocumentManagerSubmission(
    Guid SubmissionId,
    int DeletedDraftCount,
    int DeletedResultCount,
    int DeletedVisualAssetCount,
    int DeletedManifestCount);

public interface IDocumentManagerEditorialAdministrationStore
{
    Task<PurgedDocumentManagerSubmission> PurgeSubmissionAsync(
        PurgeDocumentManagerSubmissionCommand command,
        CancellationToken cancellationToken);
}

public sealed class PurgeDocumentManagerSubmissionHandler(
    IDocumentManagerEditorialAdministrationStore store,
    IDocumentManagerAdministrationAuthorizer authorizer)
{
    public Task<PurgedDocumentManagerSubmission> HandleAsync(
        PurgeDocumentManagerSubmissionCommand command,
        CancellationToken cancellationToken)
    {
        ReopenDocumentManagerEditorialDraftHandler.EnsureAuthorized(
            authorizer);
        ArgumentNullException.ThrowIfNull(command);

        if (command.DraftId == Guid.Empty || command.ExpectedVersion < 0)
        {
            throw new ArgumentException(
                "Draft identifier and expected version are invalid.",
                nameof(command));
        }

        return store.PurgeSubmissionAsync(command, cancellationToken);
    }
}
