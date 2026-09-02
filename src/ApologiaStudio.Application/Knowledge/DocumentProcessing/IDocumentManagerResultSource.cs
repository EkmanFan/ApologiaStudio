namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public interface IDocumentManagerResultSource
{
    Task<DocumentManagerResultClaim?> ClaimNextAsync(
        CancellationToken cancellationToken);

    Task<byte[]> ReadContentAsync(
        DocumentManagerResultClaim claim,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentManagerVisualAssetDescriptor>>
        ListVisualAssetsAsync(
            DocumentManagerResultClaim claim,
            CancellationToken cancellationToken);

    Task<byte[]> ReadVisualAssetAsync(
        DocumentManagerResultClaim claim,
        DocumentManagerVisualAssetDescriptor visualAsset,
        CancellationToken cancellationToken);

    Task AcknowledgeAsync(
        DocumentManagerResultClaim claim,
        CancellationToken cancellationToken);
}
