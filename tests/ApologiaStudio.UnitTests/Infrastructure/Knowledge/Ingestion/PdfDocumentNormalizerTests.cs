using ApologiaStudio.Application.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Knowledge.Ingestion;

namespace ApologiaStudio.UnitTests.Infrastructure.Knowledge.Ingestion;

public sealed class PdfDocumentNormalizerTests
{
    [Fact]
    public void Normalize_ShouldNormalizeTextWithoutLosingSourceText()
    {
        var sourceText =
            "Cafe\u0301   inter-\nnational\r\nstudy";

        var document = CreateDocument(
            CreatePage(
                1,
                CreateBlock(
                    0,
                    sourceText,
                    bottom: 300,
                    top: 330)));

        var normalizer = new PdfDocumentNormalizer();

        var result = normalizer.Normalize(
            document,
            CancellationToken.None);

        var block = Assert.Single(
            Assert.Single(result.Pages).Blocks);

        Assert.Equal(sourceText, block.SourceText);
        Assert.Equal(
            "Café international study",
            block.Text);
        Assert.False(block.IsExcluded);
        Assert.Null(block.ExclusionReason);
        Assert.Equal(
            PdfDocumentNormalizer.NormalizationProfileId,
            result.NormalizationProfileId);
    }

    [Fact]
    public void Normalize_ShouldMarkRecurringHeadersAndFooters()
    {
        var pages = Enumerable
            .Range(1, 5)
            .Select(pageNumber =>
                CreatePage(
                    pageNumber,
                    CreateBlock(
                        0,
                        "RUNNING HEADER",
                        bottom: 770,
                        top: 790),
                    CreateBlock(
                        1,
                        $"Body text for page {pageNumber}.",
                        bottom: 350,
                        top: 390),
                    CreateBlock(
                        2,
                        pageNumber.ToString(),
                        bottom: 10,
                        top: 30)))
            .ToArray();

        var document = CreateDocument(pages);
        var normalizer = new PdfDocumentNormalizer();

        var result = normalizer.Normalize(
            document,
            CancellationToken.None);

        Assert.All(
            result.Pages,
            page =>
            {
                var header = Assert.Single(
                    page.Blocks,
                    block =>
                        block.Text == "RUNNING HEADER");
                var body = Assert.Single(
                    page.Blocks,
                    block =>
                        block.Text.StartsWith(
                            "Body text",
                            StringComparison.Ordinal));
                var footer = Assert.Single(
                    page.Blocks,
                    block =>
                        block.Text.All(char.IsDigit));

                Assert.True(header.IsExcluded);
                Assert.Equal(
                    PdfBlockExclusionReason.RepeatedHeader,
                    header.ExclusionReason);

                Assert.False(body.IsExcluded);
                Assert.Null(body.ExclusionReason);

                Assert.True(footer.IsExcluded);
                Assert.Equal(
                    PdfBlockExclusionReason.RepeatedFooter,
                    footer.ExclusionReason);
            });
    }

    private static ExtractedPdfDocument CreateDocument(
        params ExtractedPdfPage[] pages) =>
        new(
            "fixture.pdf",
            new string('a', 64),
            1234,
            "fixture-extraction-v1",
            pages.Length,
            pages);

    private static ExtractedPdfPage CreatePage(
        int pageNumber,
        params ExtractedPdfTextBlock[] blocks) =>
        new(
            pageNumber,
            600,
            800,
            Array.Empty<ExtractedPdfWord>(),
            blocks);

    private static ExtractedPdfTextBlock CreateBlock(
        int readingOrder,
        string text,
        double bottom,
        double top) =>
        new(
            readingOrder,
            text,
            new PdfBoundingBox(
                50,
                bottom,
                550,
                top),
            PdfTextOrientation.Horizontal,
            "FixtureFont",
            10,
            1,
            Math.Max(
                1,
                text.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries).Length),
            readingOrder * 10,
            readingOrder * 10 + 1);
}
