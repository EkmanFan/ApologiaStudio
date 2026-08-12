using System.Security.Cryptography;
using ApologiaStudio.Application.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Knowledge.Ingestion;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ApologiaStudio.UnitTests.Infrastructure.Knowledge.Ingestion;

public sealed class PdfPigDocumentExtractorTests
{
    [Fact]
    public async Task ExtractAsync_ShouldPreserveDocumentAndLayoutMetadata()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var path = Path.Combine(
                directory,
                "generic-extraction-fixture.pdf");
            var bytes = CreateFixturePdf();
            await File.WriteAllBytesAsync(path, bytes);

            var expectedSha256 = Convert
                .ToHexString(SHA256.HashData(bytes))
                .ToLowerInvariant();

            var extractor = new PdfPigDocumentExtractor();

            var result = await extractor.ExtractAsync(
                path,
                CancellationToken.None);

            Assert.Equal(
                "generic-extraction-fixture.pdf",
                result.SourceFileName);
            Assert.Equal(expectedSha256, result.SourceSha256);
            Assert.Equal(bytes.LongLength, result.SourceByteLength);
            Assert.Equal(
                PdfPigDocumentExtractor.ExtractionProfileId,
                result.ExtractionProfileId);
            Assert.Equal(2, result.PageCount);
            Assert.Equal(2, result.Pages.Count);

            var firstPage = result.Pages[0];
            Assert.Equal(1, firstPage.PageNumber);
            Assert.True(firstPage.Width > 0);
            Assert.True(firstPage.Height > 0);
            Assert.NotEmpty(firstPage.Words);
            Assert.NotEmpty(firstPage.Blocks);

            var chapterWord = Assert.Single(
                firstPage.Words,
                word =>
                    string.Equals(
                        word.Text,
                        "Chapter",
                        StringComparison.Ordinal));
            Assert.Equal(PdfTextOrientation.Horizontal, chapterWord.Orientation);
            Assert.NotNull(chapterWord.MedianPointSize);
            Assert.True(chapterWord.MedianPointSize > 15);
            Assert.True(chapterWord.BoundingBox.Width > 0);
            Assert.True(chapterWord.BoundingBox.Height > 0);

            var firstPageText = string.Join(
                "\n",
                firstPage.Blocks.Select(block => block.Text));
            Assert.Contains("Chapter One", firstPageText);
            Assert.Contains("Alpha beta gamma.", firstPageText);

            Assert.All(
                firstPage.Blocks,
                block =>
                {
                    Assert.True(block.ReadingOrder >= 0);
                    Assert.True(block.BoundingBox.Width > 0);
                    Assert.True(block.BoundingBox.Height > 0);
                    Assert.True(block.WordCount > 0);
                    Assert.True(block.LineCount > 0);
                });

            var secondPageText = string.Join(
                "\n",
                result.Pages[1].Blocks.Select(block => block.Text));
            Assert.Contains("Second page text.", secondPageText);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAsync_ShouldRejectNonPdfSource()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var path = Path.Combine(directory, "not-a-pdf.txt");
            await File.WriteAllTextAsync(path, "not a pdf");

            var extractor = new PdfPigDocumentExtractor();

            var exception =
                await Assert.ThrowsAsync<PdfDocumentExtractionException>(
                    () => extractor.ExtractAsync(
                        path,
                        CancellationToken.None));

            Assert.Contains(
                "must be a PDF",
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAsync_ShouldHonorPreCanceledToken()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var extractor = new PdfPigDocumentExtractor();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractor.ExtractAsync(
                "unused.pdf",
                cancellationSource.Token));
    }

    private static byte[] CreateFixturePdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(
            Standard14Font.Helvetica);

        var firstPage = builder.AddPage(PageSize.A4);
        firstPage.AddText(
            "Chapter One",
            18,
            new PdfPoint(50, 780),
            font);
        firstPage.AddText(
            "Alpha beta gamma.",
            11,
            new PdfPoint(50, 740),
            font);
        firstPage.AddText(
            "Layout metadata remains available.",
            11,
            new PdfPoint(50, 720),
            font);

        var secondPage = builder.AddPage(PageSize.A4);
        secondPage.AddText(
            "Second page text.",
            12,
            new PdfPoint(50, 780),
            font);

        return builder.Build();
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"apologia-pdf-extraction-{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);
        return path;
    }
}
