using ApologiaStudio.Application.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Knowledge.Ingestion;

namespace ApologiaStudio.UnitTests.Infrastructure.Knowledge.Ingestion;

public sealed class HeuristicDocumentSegmenterTests
{
    [Fact]
    public void Segment_ShouldUseFontHierarchyAndExactKindHints()
    {
        var document = CreateDocument(
            CreateBlock(
                0,
                "A LARGE HEADING",
                pointSize: 18,
                wordCount: 3),
            CreateBlock(
                1,
                "Ordinary chapter body with enough words.",
                pointSize: 10,
                wordCount: 7),
            CreateBlock(
                2,
                "PRACTICE EXERCISE",
                pointSize: 14,
                wordCount: 2),
            CreateBlock(
                3,
                "Consider the evidence and formulate a response.",
                pointSize: 10,
                wordCount: 8),
            CreateBlock(
                4,
                "REFERENCES",
                pointSize: 14,
                wordCount: 1),
            CreateBlock(
                5,
                "Example Author. Example Work.",
                pointSize: 10,
                wordCount: 4));

        var hints = new DocumentSegmentationHints(
        [
            new HeadingSegmentKindHint(
                "PRACTICE EXERCISE",
                DocumentSegmentKind.PedagogicalPrompt),
            new HeadingSegmentKindHint(
                "REFERENCES",
                DocumentSegmentKind.Bibliography)
        ]);

        var segmenter = new HeuristicDocumentSegmenter();

        var result = segmenter.Segment(
            document,
            hints,
            CancellationToken.None);

        Assert.Equal(3, result.Segments.Count);

        var main = result.Segments[0];
        Assert.Equal(
            DocumentSegmentType.Chapter,
            main.Type);
        Assert.Equal(
            DocumentSegmentKind.MainText,
            main.Kind);
        Assert.Equal(
            "A LARGE HEADING",
            main.Title);
        Assert.Contains(
            "Ordinary chapter body",
            main.Text,
            StringComparison.Ordinal);

        var exercise = result.Segments[1];
        Assert.Equal(
            DocumentSegmentKind.PedagogicalPrompt,
            exercise.Kind);
        Assert.Equal(
            "PRACTICE EXERCISE",
            exercise.Title);
        Assert.Equal(2, exercise.SourceBlocks.Count);

        var bibliography = result.Segments[2];
        Assert.Equal(
            DocumentSegmentKind.Bibliography,
            bibliography.Kind);
        Assert.Equal(
            "REFERENCES",
            bibliography.Title);
        Assert.Equal(
            HeuristicDocumentSegmenter.SegmentationProfileId,
            result.SegmentationProfileId);
    }

    [Fact]
    public void Segment_ShouldIgnoreExcludedBlocksAndKeepUnheadedBodyAsMainText()
    {
        var document = CreateDocument(
            CreateBlock(
                0,
                "RUNNING HEADER",
                pointSize: 10,
                wordCount: 2,
                isExcluded: true,
                exclusionReason:
                    PdfBlockExclusionReason.RepeatedHeader),
            CreateBlock(
                1,
                "First body paragraph.",
                pointSize: 10,
                wordCount: 3),
            CreateBlock(
                2,
                "Second body paragraph.",
                pointSize: 10,
                wordCount: 3));

        var segmenter = new HeuristicDocumentSegmenter();

        var result = segmenter.Segment(
            document,
            DocumentSegmentationHints.Empty,
            CancellationToken.None);

        var segment = Assert.Single(result.Segments);

        Assert.Equal(
            DocumentSegmentType.ParagraphGroup,
            segment.Type);
        Assert.Equal(
            DocumentSegmentKind.MainText,
            segment.Kind);
        Assert.Null(segment.Title);
        Assert.DoesNotContain(
            "RUNNING HEADER",
            segment.Text,
            StringComparison.Ordinal);
        Assert.Equal(2, segment.SourceBlocks.Count);
    }

    [Fact]
    public void Segment_ShouldRejectDuplicateHeadingHints()
    {
        var document = CreateDocument(
            CreateBlock(
                0,
                "BODY",
                pointSize: 10,
                wordCount: 1));

        var hints = new DocumentSegmentationHints(
        [
            new HeadingSegmentKindHint(
                "Practice Exercise",
                DocumentSegmentKind.PedagogicalPrompt),
            new HeadingSegmentKindHint(
                "  PRACTICE   EXERCISE ",
                DocumentSegmentKind.Sidebar)
        ]);

        var segmenter = new HeuristicDocumentSegmenter();

        Assert.Throws<ArgumentException>(
            () => segmenter.Segment(
                document,
                hints,
                CancellationToken.None));
    }

    private static NormalizedPdfDocument CreateDocument(
        params NormalizedPdfTextBlock[] blocks) =>
        new(
            "fixture.pdf",
            new string('b', 64),
            1234,
            "fixture-extraction-v1",
            "fixture-normalization-v1",
            1,
            [
                new NormalizedPdfPage(
                    1,
                    600,
                    800,
                    blocks)
            ]);

    private static NormalizedPdfTextBlock CreateBlock(
        int readingOrder,
        string text,
        double pointSize,
        int wordCount,
        bool isExcluded = false,
        PdfBlockExclusionReason? exclusionReason = null) =>
        new(
            readingOrder,
            text,
            text,
            new PdfBoundingBox(
                50,
                300 - readingOrder * 30,
                550,
                320 - readingOrder * 30),
            PdfTextOrientation.Horizontal,
            "FixtureFont",
            pointSize,
            1,
            wordCount,
            readingOrder * 10,
            readingOrder * 10 + 1,
            isExcluded,
            exclusionReason);
}
