using ApologiaStudio.Application.Knowledge.Ingestion;

namespace ApologiaStudio.UnitTests.Application.Knowledge.Ingestion;

public sealed class KnowledgeRetrievalChunkBuilderTests
{
    [Fact]
    public void Build_ShouldExcludeNonEvidentialSegmentKindsByDefault()
    {
        var package = CreatePackage(
            new KnowledgeImportSegment(
                Guid.NewGuid(),
                "approved",
                Guid.Empty,
                null,
                DocumentSegmentType.ParagraphGroup,
                DocumentSegmentKind.MainText,
                0,
                null,
                "Main evidence text that should be retrievable.",
                "page 1"),
            new KnowledgeImportSegment(
                Guid.NewGuid(),
                "approved",
                Guid.Empty,
                null,
                DocumentSegmentType.Section,
                DocumentSegmentKind.PedagogicalPrompt,
                1,
                "Exercise",
                "Prompt text that must not enter ordinary retrieval.",
                "page 2"),
            new KnowledgeImportSegment(
                Guid.NewGuid(),
                "approved",
                Guid.Empty,
                null,
                DocumentSegmentType.ParagraphGroup,
                DocumentSegmentKind.Unknown,
                2,
                null,
                "Unclassified text that must fail safe.",
                "page 3"));

        var chunks = KnowledgeRetrievalChunkBuilder.Build(
            package,
            CreateProfile());

        var chunk = Assert.Single(chunks);
        Assert.Equal(
            DocumentSegmentKind.MainText,
            package.Segments.Single(
                segment => segment.Id == chunk.SegmentId).SegmentKind);
        Assert.Equal(
            "Main evidence text that should be retrievable.",
            chunk.Text);
    }

    [Fact]
    public void Build_ShouldRejectNormalizedArtifactThatIsNotApproved()
    {
        var package = CreatePackage(
            new KnowledgeImportSegment(
                Guid.NewGuid(),
                "approved",
                Guid.Empty,
                null,
                DocumentSegmentType.ParagraphGroup,
                DocumentSegmentKind.MainText,
                0,
                null,
                "Approved segment text.",
                "page 1"));

        package = package with
        {
            Artifacts =
            [
                package.Artifacts[0] with
                {
                    EditorialReviewStatus = "pending"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => KnowledgeRetrievalChunkBuilder.Build(
                package,
                CreateProfile()));

        Assert.Contains(
            "active, editorially approved",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ShouldPreserveExactSegmentSubstringAndCoverage()
    {
        var text = string.Join(
            " ",
            Enumerable.Repeat(
                "Evidence sentence.",
                50));

        var package = CreatePackage(
            new KnowledgeImportSegment(
                Guid.NewGuid(),
                "approved",
                Guid.Empty,
                null,
                DocumentSegmentType.ParagraphGroup,
                DocumentSegmentKind.MainText,
                0,
                null,
                text,
                "page 1"));

        var profile = CreateProfile() with
        {
            MaxChunkCharacters = 180,
            OverlapCharacters = 30,
            BoundarySearchCharacters = 60,
            MinimumPreferredChunkCharacters = 100
        };

        var chunks = KnowledgeRetrievalChunkBuilder.Build(
            package,
            profile);

        Assert.True(chunks.Count > 1);
        Assert.Equal(0, chunks[0].StartOffset);
        Assert.Equal(text.Length, chunks[^1].EndOffset);

        Assert.All(
            chunks,
            chunk => Assert.Equal(
                text[chunk.StartOffset..chunk.EndOffset],
                chunk.Text));
    }

    private static KnowledgeImportPackage CreatePackage(
        params KnowledgeImportSegment[] segments)
    {
        var workId = Guid.NewGuid();
        var expressionId = Guid.NewGuid();
        var manifestationId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        byte[] bytes = [1, 2, 3];

        var reboundSegments = segments
            .Select(segment => segment with
            {
                ArtifactId = artifactId
            })
            .ToArray();

        return new KnowledgeImportPackage(
            "retrieval-fixture-v1",
            "retrieval-fixture",
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
                    "Fixture")
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
                    "text/plain",
                    bytes.LongLength,
                    null,
                    "active",
                    ".txt",
                    null,
                    bytes)
            ],
            Array.Empty<KnowledgeImportProcessingActivity>(),
            reboundSegments,
            Array.Empty<KnowledgeImportClassificationTerm>(),
            Array.Empty<KnowledgeImportClassificationAssertion>(),
            Array.Empty<KnowledgeImportMetadataAssertion>());
    }

    private static KnowledgeRetrievalProfile CreateProfile() =>
        new(
            "unit-retrieval-v1",
            "segment-character-window",
            "v1",
            1_000,
            100,
            100,
            200,
            "unit-test",
            "unit-test-model",
            3);
}
