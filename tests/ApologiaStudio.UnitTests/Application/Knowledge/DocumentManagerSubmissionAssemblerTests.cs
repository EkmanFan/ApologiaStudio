using ApologiaStudio.Application.Knowledge.DocumentProcessing;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

public sealed class DocumentManagerSubmissionAssemblerTests
{
    [Fact]
    public void Assemble_GroupsReceivedPartsInManifestOrder()
    {
        var manifest = CreatePageManifest();
        var first = manifest.ExpectedUnits[0];
        var second = manifest.ExpectedUnits[1];

        var assembly =
            DocumentManagerSubmissionAssembler.Assemble(
                manifest,
                [
                    Result("result-2", second),
                    Result("result-1", first)
                ]);

        Assert.Equal(
            DocumentManagerSubmissionAssemblyStatus.Ready,
            assembly.Status);
        Assert.Equal(2, assembly.ReceivedPartCount);
        Assert.Equal("result-1", assembly.Parts[0].ResultReference);
        Assert.Equal("result-2", assembly.Parts[1].ResultReference);
        Assert.Empty(assembly.Issues);
    }

    [Fact]
    public void Assemble_WaitsWhenOneExpectedPartIsMissing()
    {
        var manifest = CreatePageManifest();

        var assembly =
            DocumentManagerSubmissionAssembler.Assemble(
                manifest,
                [Result("result-1", manifest.ExpectedUnits[0])]);

        Assert.Equal(
            DocumentManagerSubmissionAssemblyStatus.AwaitingParts,
            assembly.Status);
        Assert.Equal(1, assembly.ReceivedPartCount);
        Assert.Equal(2, assembly.ExpectedPartCount);
        Assert.Null(assembly.Parts[1].ResultReference);
    }

    [Fact]
    public void Assemble_BlocksDiscontinuousRanges()
    {
        var submissionId = Guid.NewGuid();
        var first = Expected(
            1,
            new DocumentManagerResultScope(
                "pageRange", 1, 50, "Part 1",
                null, null, null, null));
        var second = Expected(
            2,
            new DocumentManagerResultScope(
                "pageRange", 52, 100, "Part 2",
                null, null, null, null));
        var manifest = Manifest(submissionId, first, second);

        var assembly =
            DocumentManagerSubmissionAssembler.Assemble(
                manifest,
                [Result("result-1", first), Result("result-2", second)]);

        Assert.Equal(
            DocumentManagerSubmissionAssemblyStatus.Blocked,
            assembly.Status);
        Assert.Contains(
            assembly.Issues,
            issue => issue.Code == "discontinuous-page-ranges");
    }

    [Fact]
    public void Assemble_BlocksResultOutsideCurrentManifest()
    {
        var manifest = CreatePageManifest();
        var unexpected = Expected(
            3,
            new DocumentManagerResultScope(
                "pageRange", 101, 150, "Old part",
                null, null, null, null));

        var assembly =
            DocumentManagerSubmissionAssembler.Assemble(
                manifest,
                [Result("old-result", unexpected)]);

        Assert.Equal(
            DocumentManagerSubmissionAssemblyStatus.Blocked,
            assembly.Status);
        Assert.Contains(
            assembly.Issues,
            issue => issue.Code == "unexpected-part");
    }

    private static DocumentManagerSubmissionManifest CreatePageManifest()
    {
        var submissionId = Guid.NewGuid();
        return Manifest(
            submissionId,
            Expected(
                1,
                new DocumentManagerResultScope(
                    "pageRange", 1, 50, "Part 1",
                    null, null, null, null)),
            Expected(
                2,
                new DocumentManagerResultScope(
                    "pageRange", 51, 100, "Part 2",
                    null, null, null, null)));
    }

    private static DocumentManagerSubmissionManifest Manifest(
        Guid submissionId,
        params DocumentManagerExpectedProcessingUnit[] units) =>
        new(
            submissionId,
            2,
            new string('a', 64),
            "book.pdf",
            new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            units);

    private static DocumentManagerExpectedProcessingUnit Expected(
        int ordinal,
        DocumentManagerResultScope scope) =>
        new(Guid.NewGuid(), ordinal, scope);

    private static DocumentManagerStoredResultSummary Result(
        string reference,
        DocumentManagerExpectedProcessingUnit unit) =>
        new(reference, unit.ProcessingUnitId, unit.Scope);
}
