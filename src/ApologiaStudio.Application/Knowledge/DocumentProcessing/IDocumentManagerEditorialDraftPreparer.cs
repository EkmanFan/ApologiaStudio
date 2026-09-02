namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public interface IDocumentManagerEditorialDraftPreparer
{
    Task<DocumentManagerEditorialDraftPreparationResult> PrepareAsync(
        Guid submissionId,
        CancellationToken cancellationToken);
}
