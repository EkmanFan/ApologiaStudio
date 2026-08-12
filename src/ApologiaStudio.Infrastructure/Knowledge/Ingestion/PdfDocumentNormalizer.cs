using System.Text;
using System.Text.RegularExpressions;
using ApologiaStudio.Application.Knowledge.Ingestion;

namespace ApologiaStudio.Infrastructure.Knowledge.Ingestion;

public sealed class PdfDocumentNormalizer : IPdfDocumentNormalizer
{
    public const string NormalizationProfileId =
        "unicode-nfc-whitespace-dehyphenation-recurring-margins-v1";

    private const double HeaderZoneFraction = 0.12;
    private const double FooterZoneFraction = 0.12;
    private const int MaximumRecurringCandidateLength = 160;

    private static readonly Regex DehyphenationRegex = new(
        @"(?<=\p{L})-[\t ]*\n[\t ]*(?=\p{Ll})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DigitRunRegex = new(
        @"\d+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public NormalizedPdfDocument Normalize(
        ExtractedPdfDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        if (document.PageCount != document.Pages.Count)
        {
            throw new InvalidOperationException(
                "Extracted PDF page count does not match the page collection.");
        }

        var provisionalPages = new List<NormalizedPdfPage>(
            document.Pages.Count);

        foreach (var page in document.Pages.OrderBy(page => page.PageNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blocks = page.Blocks
                .OrderBy(block => block.ReadingOrder)
                .Select(block => new NormalizedPdfTextBlock(
                    block.ReadingOrder,
                    block.Text,
                    NormalizeText(block.Text),
                    block.BoundingBox,
                    block.Orientation,
                    block.DominantFontName,
                    block.MedianPointSize,
                    block.LineCount,
                    block.WordCount,
                    block.FirstSourceSequence,
                    block.LastSourceSequence,
                    IsExcluded: false,
                    ExclusionReason: null))
                .ToArray();

            provisionalPages.Add(
                new NormalizedPdfPage(
                    page.PageNumber,
                    page.Width,
                    page.Height,
                    blocks));
        }

        var recurringMarginKeys = FindRecurringMarginKeys(
            provisionalPages,
            cancellationToken);

        var finalPages = provisionalPages
            .Select(page => ApplyMarginExclusions(
                page,
                recurringMarginKeys))
            .ToArray();

        return new NormalizedPdfDocument(
            document.SourceFileName,
            document.SourceSha256,
            document.SourceByteLength,
            document.ExtractionProfileId,
            NormalizationProfileId,
            document.PageCount,
            finalPages);
    }

    private static string NormalizeText(string text)
    {
        var normalized = text
            .Normalize(NormalizationForm.FormC)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        normalized = DehyphenationRegex.Replace(
            normalized,
            string.Empty);

        return WhitespaceRegex
            .Replace(normalized, " ")
            .Trim();
    }

    private static HashSet<MarginKey> FindRecurringMarginKeys(
        IReadOnlyCollection<NormalizedPdfPage> pages,
        CancellationToken cancellationToken)
    {
        var pagesByKey = new Dictionary<MarginKey, HashSet<int>>();

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var block in page.Blocks)
            {
                if (string.IsNullOrWhiteSpace(block.Text) ||
                    block.Text.Length > MaximumRecurringCandidateLength)
                {
                    continue;
                }

                var zone = GetMarginZone(page, block);
                if (zone is null)
                {
                    continue;
                }

                var recurrenceKey = CanonicalizeRecurringText(
                    block.Text);

                if (recurrenceKey.Length == 0)
                {
                    continue;
                }

                var key = new MarginKey(
                    zone.Value,
                    recurrenceKey);

                if (!pagesByKey.TryGetValue(
                        key,
                        out var pageNumbers))
                {
                    pageNumbers = [];
                    pagesByKey.Add(key, pageNumbers);
                }

                pageNumbers.Add(page.PageNumber);
            }
        }

        var minimumOccurrenceCount =
            GetMinimumOccurrenceCount(pages.Count);

        return pagesByKey
            .Where(pair =>
                pair.Value.Count >= minimumOccurrenceCount)
            .Select(pair => pair.Key)
            .ToHashSet();
    }

    private static NormalizedPdfPage ApplyMarginExclusions(
        NormalizedPdfPage page,
        IReadOnlySet<MarginKey> recurringMarginKeys)
    {
        var blocks = page.Blocks
            .Select(block =>
            {
                var zone = GetMarginZone(page, block);
                if (zone is null)
                {
                    return block;
                }

                var key = new MarginKey(
                    zone.Value,
                    CanonicalizeRecurringText(block.Text));

                if (!recurringMarginKeys.Contains(key))
                {
                    return block;
                }

                return block with
                {
                    IsExcluded = true,
                    ExclusionReason =
                        zone == MarginZone.Header
                            ? PdfBlockExclusionReason.RepeatedHeader
                            : PdfBlockExclusionReason.RepeatedFooter
                };
            })
            .ToArray();

        return page with
        {
            Blocks = blocks
        };
    }

    private static MarginZone? GetMarginZone(
        NormalizedPdfPage page,
        NormalizedPdfTextBlock block)
    {
        if (page.Height <= 0 ||
            block.BoundingBox.Height <= 0 ||
            block.BoundingBox.Height > page.Height * 0.20)
        {
            return null;
        }

        if (block.BoundingBox.Top >=
            page.Height * (1.0 - HeaderZoneFraction))
        {
            return MarginZone.Header;
        }

        if (block.BoundingBox.Bottom <=
            page.Height * FooterZoneFraction)
        {
            return MarginZone.Footer;
        }

        return null;
    }

    private static string CanonicalizeRecurringText(
        string text)
    {
        var normalized = NormalizeText(text)
            .ToUpperInvariant();

        return DigitRunRegex.Replace(
            normalized,
            "#");
    }

    private static int GetMinimumOccurrenceCount(
        int pageCount)
    {
        var proportionalCount =
            (int)Math.Ceiling(pageCount * 0.02);

        return Math.Max(
            3,
            Math.Min(10, proportionalCount));
    }

    private enum MarginZone
    {
        Header = 0,
        Footer = 1
    }

    private readonly record struct MarginKey(
        MarginZone Zone,
        string Text);
}
