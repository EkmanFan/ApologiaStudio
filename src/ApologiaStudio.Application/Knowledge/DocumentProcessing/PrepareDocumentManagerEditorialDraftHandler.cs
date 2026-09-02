namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public sealed class PrepareDocumentManagerEditorialDraftHandler(
    IDocumentManagerSubmissionAssemblyReader assemblyReader,
    IDocumentManagerEditorialDraftStore draftStore,
    TimeProvider timeProvider)
    : IDocumentManagerEditorialDraftPreparer
{
    public async Task<DocumentManagerEditorialDraftPreparationResult> PrepareAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var assembly =
            await assemblyReader.GetAsync(
                submissionId,
                cancellationToken) ??
            throw new DocumentManagerResultIntegrityException(
                $"Submission '{submissionId:D}' has no stored assembly manifest.");

        if (assembly.Status ==
            DocumentManagerSubmissionAssemblyStatus.AwaitingParts)
        {
            return new DocumentManagerEditorialDraftPreparationResult(
                DocumentManagerEditorialDraftPreparationStatus.AwaitingParts,
                assembly,
                null);
        }

        if (assembly.Status ==
            DocumentManagerSubmissionAssemblyStatus.Blocked)
        {
            return new DocumentManagerEditorialDraftPreparationResult(
                DocumentManagerEditorialDraftPreparationStatus.Blocked,
                assembly,
                null);
        }

        var candidate =
            DocumentManagerEditorialDraftFactory.Create(
                assembly,
                timeProvider.GetUtcNow());
        var writeResult =
            await draftStore.StoreAsync(
                candidate,
                cancellationToken);

        return new DocumentManagerEditorialDraftPreparationResult(
            writeResult.Status == DocumentManagerEditorialDraftWriteStatus.Created
                ? DocumentManagerEditorialDraftPreparationStatus.Created
                : DocumentManagerEditorialDraftPreparationStatus.AlreadyExists,
            assembly,
            writeResult.Draft);
    }
}
