using ApologiaStudio.Application.Knowledge.Ingestion;

namespace ApologiaStudio.UnitTests.Application.Knowledge.Ingestion;

public sealed class KnowledgeImportPackageValidatorTests
{
    [Fact]
    public void Validate_ShouldAcceptMinimalSelfContainedPackage()
    {
        var package = CreateValidPackage();

        KnowledgeImportPackageValidator.Validate(package);
    }

    [Fact]
    public void Validate_ShouldRejectDuplicateOwnedResourceIds()
    {
        var package = CreateValidPackage();
        var duplicateId = package.Works[0].Id;

        package = package with
        {
            Expressions =
            [
                package.Expressions[0] with
                {
                    Id = duplicateId
                }
            ]
        };

        var exception = Assert.Throws<
            KnowledgeImportPackageValidationException>(
            () => KnowledgeImportPackageValidator.Validate(package));

        Assert.Contains(
            "Duplicate package-owned resource id",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldRejectArtifactWithTwoPayloadSources()
    {
        var package = CreateValidPackage();
        var artifact = package.Artifacts[0];

        package = package with
        {
            Artifacts =
            [
                artifact with
                {
                    SourcePath = "/tmp/source.txt"
                }
            ]
        };

        var exception = Assert.Throws<
            KnowledgeImportPackageValidationException>(
            () => KnowledgeImportPackageValidator.Validate(package));

        Assert.Contains(
            "exactly one payload source",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldRejectBytePayloadWhoseHashDoesNotMatchIdentity()
    {
        var package = CreateValidPackage();
        var artifact = package.Artifacts[0];

        package = package with
        {
            Artifacts =
            [
                artifact with
                {
                    Sha256 = new string('f', 64)
                }
            ]
        };

        var exception = Assert.Throws<
            KnowledgeImportPackageValidationException>(
            () => KnowledgeImportPackageValidator.Validate(package));

        Assert.Contains(
            "does not match its SHA-256 identity",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldRequireKnownClassificationTerm()
    {
        var package = CreateValidPackage();

        package = package with
        {
            ClassificationAssertions =
            [
                new KnowledgeImportClassificationAssertion(
                    Guid.NewGuid(),
                    package.PrimaryWorkId,
                    KnowledgeClassificationDimension.SourceKind,
                    "missing_term",
                    null,
                    "editorial",
                    package.EditorialActor,
                    "verified",
                    package.EditorialActor,
                    null,
                    null,
                    null)
            ]
        };

        var exception = Assert.Throws<
            KnowledgeImportPackageValidationException>(
            () => KnowledgeImportPackageValidator.Validate(package));

        Assert.Contains(
            "references unknown term",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static KnowledgeImportPackage CreateValidPackage()
    {
        var workId = Guid.NewGuid();
        var expressionId = Guid.NewGuid();
        var manifestationId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var segmentId = Guid.NewGuid();
        byte[] bytes = [1, 2, 3];

        return new KnowledgeImportPackage(
            "fixture-profile-v1",
            "fixture-profile",
            workId,
            artifactId,
            "unit-test",
            [
                new KnowledgeImportWork(
                    workId,
                    "approved",
                    "Fixture work",
                    "en",
                    null)
            ],
            [
                new KnowledgeImportExpression(
                    expressionId,
                    "approved",
                    workId,
                    "en",
                    "Fixture expression",
                    null)
            ],
            Array.Empty<KnowledgeImportExpressionRelation>(),
            [
                new KnowledgeImportManifestation(
                    manifestationId,
                    "approved",
                    expressionId,
                    "Fixture edition",
                    2026,
                    null,
                    "Fixture citation")
            ],
            Array.Empty<KnowledgeImportManifestationIdentifier>(),
            Array.Empty<KnowledgeImportContributor>(),
            Array.Empty<KnowledgeImportContribution>(),
            [
                new KnowledgeImportArtifact(
                    artifactId,
                    "approved",
                    manifestationId,
                    null,
                    "normalized",
                    "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81",
                    "text/plain; charset=utf-8",
                    bytes.LongLength,
                    null,
                    "active",
                    ".txt",
                    null,
                    bytes)
            ],
            Array.Empty<KnowledgeImportProcessingActivity>(),
            [
                new KnowledgeImportSegment(
                    segmentId,
                    "approved",
                    artifactId,
                    null,
                    DocumentSegmentType.ParagraphGroup,
                    DocumentSegmentKind.MainText,
                    0,
                    null,
                    "Fixture evidence text.",
                    "page 1")
            ],
            Array.Empty<KnowledgeImportClassificationTerm>(),
            Array.Empty<KnowledgeImportClassificationAssertion>(),
            Array.Empty<KnowledgeImportMetadataAssertion>());
    }
}
