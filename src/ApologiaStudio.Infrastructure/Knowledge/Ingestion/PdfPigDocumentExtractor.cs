using System.Security.Cryptography;
using ApologiaStudio.Application.Knowledge.Ingestion;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace ApologiaStudio.Infrastructure.Knowledge.Ingestion;

public sealed class PdfPigDocumentExtractor : IPdfDocumentExtractor
{
    public const string ExtractionProfileId =
        "pdfpig-0.1.15-nearest-neighbour-docstrum-unsupervised-raster-metadata-v2";

    public async Task<ExtractedPdfDocument> ExtractAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            throw new PdfDocumentExtractionException(
                $"Source PDF was not found: {fullPath}");
        }

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfDocumentExtractionException(
                "The source artifact must be a PDF file.");
        }

        try
        {
            var initialFileInfo = new FileInfo(fullPath);
            if (initialFileInfo.Length <= 0)
            {
                throw new PdfDocumentExtractionException(
                    "The source PDF is empty.");
            }

            var initialSha256 = await ComputeFileSha256Async(
                fullPath,
                cancellationToken);

            var pages = new List<ExtractedPdfPage>();
            int pageCount;

            using (var document = PdfDocument.Open(fullPath))
            {
                pageCount = document.NumberOfPages;
                if (pageCount <= 0)
                {
                    throw new PdfDocumentExtractionException(
                        "The source PDF contains no pages.");
                }

                pages.Capacity = pageCount;

                for (var pageNumber = 1;
                     pageNumber <= pageCount;
                     pageNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var page = document.GetPage(pageNumber);
                    pages.Add(ExtractPage(pageNumber, page));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            var finalFileInfo = new FileInfo(fullPath);
            var finalSha256 = await ComputeFileSha256Async(
                fullPath,
                cancellationToken);

            if (finalFileInfo.Length != initialFileInfo.Length ||
                !string.Equals(
                    finalSha256,
                    initialSha256,
                    StringComparison.Ordinal))
            {
                throw new PdfDocumentExtractionException(
                    "The source PDF changed while it was being extracted.");
            }

            return new ExtractedPdfDocument(
                initialFileInfo.Name,
                initialSha256,
                initialFileInfo.Length,
                ExtractionProfileId,
                pageCount,
                pages);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PdfDocumentExtractionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PdfDocumentExtractionException(
                $"Failed to extract PDF '{Path.GetFileName(fullPath)}'.",
                exception);
        }
    }

    private static ExtractedPdfPage ExtractPage(
        int pageNumber,
        Page page)
    {
        var pdfWords = page
            .GetWords(NearestNeighbourWordExtractor.Instance)
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .ToArray();

        var words = new ExtractedPdfWord[pdfWords.Length];
        for (var index = 0; index < pdfWords.Length; index++)
        {
            words[index] = ConvertWord(index, pdfWords[index]);
        }

        var pageWidth = Convert.ToDouble(page.Width);
        var pageHeight = Convert.ToDouble(page.Height);
        var rasterImageBounds = page
            .GetImages()
            .Select(image =>
                ToAxisAlignedBoundingBox(image.BoundingBox))
            .ToArray();
        var largestRasterImageAreaRatio =
            GetLargestImageAreaRatio(
                pageWidth,
                pageHeight,
                rasterImageBounds);

        if (pdfWords.Length == 0)
        {
            return new ExtractedPdfPage(
                pageNumber,
                pageWidth,
                pageHeight,
                words,
                Array.Empty<ExtractedPdfTextBlock>())
            {
                RasterImageCount =
                    rasterImageBounds.Length,
                LargestRasterImageAreaRatio =
                    largestRasterImageAreaRatio
            };
        }

        var segmentedBlocks =
            DocstrumBoundingBoxes.Instance.GetBlocks(pdfWords);
        var orderedBlocks =
            UnsupervisedReadingOrderDetector.Instance
                .Get(segmentedBlocks)
                .ToArray();

        var blocks = new ExtractedPdfTextBlock[orderedBlocks.Length];
        for (var index = 0; index < orderedBlocks.Length; index++)
        {
            blocks[index] = ConvertBlock(index, orderedBlocks[index]);
        }

        return new ExtractedPdfPage(
            pageNumber,
            pageWidth,
            pageHeight,
            words,
            blocks)
        {
            RasterImageCount =
                rasterImageBounds.Length,
            LargestRasterImageAreaRatio =
                largestRasterImageAreaRatio
        };
    }

    private static double GetLargestImageAreaRatio(
        double pageWidth,
        double pageHeight,
        IReadOnlyCollection<PdfBoundingBox> imageBounds)
    {
        var pageArea = pageWidth * pageHeight;
        if (pageArea <= 0 ||
            imageBounds.Count == 0)
        {
            return 0;
        }

        return imageBounds
            .Select(bounds =>
                Math.Max(0, bounds.Width) *
                Math.Max(0, bounds.Height) /
                pageArea)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static ExtractedPdfWord ConvertWord(
        int ordinal,
        Word word)
    {
        var sourceSequence = word.Letters.Count == 0
            ? ordinal
            : word.Letters.Min(letter => letter.TextSequence);

        return new ExtractedPdfWord(
            ordinal,
            sourceSequence,
            word.Text,
            ToAxisAlignedBoundingBox(word.BoundingBox),
            ToOrientation(word.TextOrientation),
            word.FontName,
            GetMedianPointSize(word.Letters));
    }

    private static ExtractedPdfTextBlock ConvertBlock(
        int fallbackReadingOrder,
        TextBlock block)
    {
        var blockWords = block.TextLines
            .SelectMany(line => line.Words)
            .ToArray();
        var letters = blockWords
            .SelectMany(word => word.Letters)
            .ToArray();

        var sourceSequences = letters
            .Select(letter => letter.TextSequence)
            .ToArray();

        return new ExtractedPdfTextBlock(
            block.ReadingOrder >= 0
                ? block.ReadingOrder
                : fallbackReadingOrder,
            block.Text,
            ToAxisAlignedBoundingBox(block.BoundingBox),
            ToOrientation(block.TextOrientation),
            GetDominantFontName(letters),
            GetMedianPointSize(letters),
            block.TextLines.Count,
            blockWords.Length,
            sourceSequences.Length == 0
                ? null
                : sourceSequences.Min(),
            sourceSequences.Length == 0
                ? null
                : sourceSequences.Max());
    }

    private static PdfBoundingBox ToAxisAlignedBoundingBox(
        PdfRectangle rectangle)
    {
        var left = Math.Min(
            Math.Min(rectangle.BottomLeft.X, rectangle.BottomRight.X),
            Math.Min(rectangle.TopLeft.X, rectangle.TopRight.X));
        var right = Math.Max(
            Math.Max(rectangle.BottomLeft.X, rectangle.BottomRight.X),
            Math.Max(rectangle.TopLeft.X, rectangle.TopRight.X));
        var bottom = Math.Min(
            Math.Min(rectangle.BottomLeft.Y, rectangle.BottomRight.Y),
            Math.Min(rectangle.TopLeft.Y, rectangle.TopRight.Y));
        var top = Math.Max(
            Math.Max(rectangle.BottomLeft.Y, rectangle.BottomRight.Y),
            Math.Max(rectangle.TopLeft.Y, rectangle.TopRight.Y));

        return new PdfBoundingBox(
            left,
            bottom,
            right,
            top);
    }

    private static PdfTextOrientation ToOrientation(
        TextOrientation orientation) =>
        orientation switch
        {
            TextOrientation.Horizontal =>
                PdfTextOrientation.Horizontal,
            TextOrientation.Rotate90 =>
                PdfTextOrientation.Rotate90,
            TextOrientation.Rotate180 =>
                PdfTextOrientation.Rotate180,
            TextOrientation.Rotate270 =>
                PdfTextOrientation.Rotate270,
            _ =>
                PdfTextOrientation.Other
        };

    private static string? GetDominantFontName(
        IReadOnlyCollection<Letter> letters) =>
        letters
            .Select(letter => letter.FontName)
            .OfType<string>()
            .Where(fontName => !string.IsNullOrWhiteSpace(fontName))
            .GroupBy(fontName => fontName, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();

    private static double? GetMedianPointSize(
        IReadOnlyCollection<Letter> letters)
    {
        var values = letters
            .Select(letter => letter.PointSize)
            .Where(value => value > 0 && double.IsFinite(value))
            .OrderBy(value => value)
            .ToArray();

        if (values.Length == 0)
        {
            return null;
        }

        var middle = values.Length / 2;
        return values.Length % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2.0
            : values[middle];
    }

    private static async Task<string> ComputeFileSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var digest = await SHA256.HashDataAsync(
            stream,
            cancellationToken);

        return Convert
            .ToHexString(digest)
            .ToLowerInvariant();
    }
}
