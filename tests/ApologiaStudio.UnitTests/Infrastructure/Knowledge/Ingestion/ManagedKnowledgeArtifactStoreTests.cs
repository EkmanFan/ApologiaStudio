using System.Security.Cryptography;
using ApologiaStudio.Application.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Knowledge.Ingestion;

namespace ApologiaStudio.UnitTests.Infrastructure.Knowledge.Ingestion;

public sealed class ManagedKnowledgeArtifactStoreTests
{
    [Fact]
    public async Task MaterializeAsync_ShouldVerifyAndReuseDeclaredArtifacts()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"apologia-managed-artifacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var sourcePath = Path.Combine(
                directory,
                "source.pdf");
            byte[] sourceBytes = [37, 80, 68, 70, 45, 49];
            byte[] normalizedBytes = [1, 2, 3];

            await File.WriteAllBytesAsync(
                sourcePath,
                sourceBytes);

            var package = CreatePackage(
                sourcePath,
                sourceBytes,
                normalizedBytes);
            var artifactRoot = Path.Combine(
                directory,
                "managed");

            var first = await ManagedKnowledgeArtifactStore.MaterializeAsync(
                package,
                artifactRoot,
                CancellationToken.None);

            Assert.Equal(2, first.CreatedPaths.Count);
            Assert.All(
                package.Artifacts,
                artifact => Assert.True(
                    File.Exists(
                        first.GetRequiredPath(artifact.Id))));

            var second = await ManagedKnowledgeArtifactStore.MaterializeAsync(
                package,
                artifactRoot,
                CancellationToken.None);

            Assert.Empty(second.CreatedPaths);
            Assert.Equal(
                first.PathsByArtifactId.Count,
                second.PathsByArtifactId.Count);
            Assert.All(
                first.PathsByArtifactId,
                pair => Assert.Equal(
                    pair.Value,
                    second.GetRequiredPath(pair.Key)));
        }
        finally
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }

    private static KnowledgeImportPackage CreatePackage(
        string sourcePath,
        byte[] sourceBytes,
        byte[] normalizedBytes)
    {
        var workId = Guid.NewGuid();
        var expressionId = Guid.NewGuid();
        var manifestationId = Guid.NewGuid();
        var rawArtifactId = Guid.NewGuid();
        var normalizedArtifactId = Guid.NewGuid();

        return new KnowledgeImportPackage(
            "managed-artifact-fixture-v1",
            "managed-artifact-fixture",
            workId,
            normalizedArtifactId,
            "unit-test",
            [
                new KnowledgeImportWork(
                    workId,
                    "approved",
                    "Managed artifact fixture",
                    "en",
                    null)
            ],
            [
                new KnowledgeImportExpression(
                    expressionId,
                    "approved",
                    workId,
                    "en",
                    null,
                    null)
            ],
            Array.Empty<KnowledgeImportExpressionRelation>(),
            [
                new KnowledgeImportManifestation(
                    manifestationId,
                    "approved",
                    expressionId,
                    null,
                    null,
                    null,
                    "Managed artifact fixture")
            ],
            Array.Empty<KnowledgeImportManifestationIdentifier>(),
            Array.Empty<KnowledgeImportContributor>(),
            Array.Empty<KnowledgeImportContribution>(),
            [
                new KnowledgeImportArtifact(
                    rawArtifactId,
                    "approved",
                    manifestationId,
                    null,
                    "raw",
                    Sha256(sourceBytes),
                    "application/pdf",
                    sourceBytes.LongLength,
                    null,
                    "active",
                    ".pdf",
                    sourcePath,
                    null),
                new KnowledgeImportArtifact(
                    normalizedArtifactId,
                    "approved",
                    manifestationId,
                    rawArtifactId,
                    "normalized",
                    Sha256(normalizedBytes),
                    "text/plain",
                    normalizedBytes.LongLength,
                    null,
                    "active",
                    ".txt",
                    null,
                    normalizedBytes)
            ],
            Array.Empty<KnowledgeImportProcessingActivity>(),
            Array.Empty<KnowledgeImportSegment>(),
            Array.Empty<KnowledgeImportClassificationTerm>(),
            Array.Empty<KnowledgeImportClassificationAssertion>(),
            Array.Empty<KnowledgeImportMetadataAssertion>());
    }

    private static string Sha256(byte[] bytes) =>
        Convert
            .ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();
}
