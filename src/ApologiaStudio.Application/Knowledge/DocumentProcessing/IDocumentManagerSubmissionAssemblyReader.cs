namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public interface IDocumentManagerSubmissionAssemblyReader
{
    Task<DocumentManagerSubmissionAssembly?> GetAsync(
        Guid submissionId,
        CancellationToken cancellationToken);
}
