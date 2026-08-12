using System.Text.RegularExpressions;
using ApologiaStudio.Application.Knowledge.Ingestion;

namespace ApologiaStudio.Infrastructure.Knowledge.Ingestion;

public sealed class HeuristicDocumentSegmenter : IDocumentSegmenter
{
    public const string SegmentationProfileId =
        "font-hierarchy-source-line-compact-hints-page-fallback-v3";

    private const int MaximumHeadingCharacters = 180;
    private const int MaximumHeadingWords = 24;
    private const double MinimumHeadingFontRatio = 1.18;
    private const double SectionFontRatio = 1.30;
    private const double ChapterFontRatio = 1.55;

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public DocumentSegmentationResult Segment(
        NormalizedPdfDocument document,
        DocumentSegmentationHints? hints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        if (document.PageCount != document.Pages.Count)
        {
            throw new InvalidOperationException(
                "Normalized PDF page count does not match the page collection.");
        }

        var headingKinds = BuildHeadingKindMap(
            hints ?? DocumentSegmentationHints.Empty);

        var blocks = document.Pages
            .OrderBy(page => page.PageNumber)
            .SelectMany(page =>
                page.Blocks
                    .Where(block =>
                        !block.IsExcluded &&
                        !string.IsNullOrWhiteSpace(block.Text))
                    .OrderBy(block => block.ReadingOrder)
                    .Select(block => new BlockContext(
                        page.PageNumber,
                        block)))
            .ToArray();

        var bodyFontSize = GetWeightedMedianFontSize(
            blocks);

        var segments = new List<DocumentSegmentDraft>();
        SegmentAccumulator? current = null;

        foreach (var block in blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (current is
                {
                    Type: DocumentSegmentType.ParagraphGroup
                } &&
                current.Blocks.Count > 0 &&
                current.Blocks[^1].PageNumber !=
                block.PageNumber)
            {
                FlushCurrent(
                    segments,
                    ref current);
            }

            var hasKindHint = TryResolveHeadingKind(
                block.Block,
                headingKinds,
                out var hintedKind,
                out var hintedTitle);

            var isHeading =
                hasKindHint ||
                (HasMinimumHeadingTextQuality(
                     block.Block.Text) &&
                 IsHeadingCandidate(
                     block.Block,
                     bodyFontSize));

            if (isHeading)
            {
                FlushCurrent(
                    segments,
                    ref current);

                current = new SegmentAccumulator(
                    DetermineSegmentType(
                        block.Block,
                        bodyFontSize),
                    hasKindHint
                        ? hintedKind
                        : DocumentSegmentKind.MainText,
                    hasKindHint
                        ? hintedTitle
                        : block.Block.Text);

                current.Add(block);
                continue;
            }

            current ??= new SegmentAccumulator(
                DocumentSegmentType.ParagraphGroup,
                DocumentSegmentKind.MainText,
                title: null);

            current.Add(block);
        }

        FlushCurrent(
            segments,
            ref current);

        return new DocumentSegmentationResult(
            document.SourceFileName,
            document.SourceSha256,
            document.SourceByteLength,
            document.ExtractionProfileId,
            document.NormalizationProfileId,
            SegmentationProfileId,
            segments);
    }

    private static Dictionary<string, DocumentSegmentKind>
        BuildHeadingKindMap(
            DocumentSegmentationHints hints)
    {
        var result = new Dictionary<
            string,
            DocumentSegmentKind>(
            StringComparer.Ordinal);

        foreach (var hint in hints.HeadingSegmentKinds)
        {
            if (string.IsNullOrWhiteSpace(hint.HeadingText))
            {
                throw new ArgumentException(
                    "Heading hints cannot contain an empty heading.",
                    nameof(hints));
            }

            var key = NormalizeHeadingKey(
                hint.HeadingText);

            if (!result.TryAdd(
                    key,
                    hint.Kind))
            {
                throw new ArgumentException(
                    $"Duplicate heading hint: '{hint.HeadingText}'.",
                    nameof(hints));
            }
        }

        return result;
    }

    private static bool TryResolveHeadingKind(
        NormalizedPdfTextBlock block,
        IReadOnlyDictionary<string, DocumentSegmentKind> headingKinds,
        out DocumentSegmentKind kind,
        out string title)
    {
        if (TryResolveHeadingKindFromText(
                block.Text,
                headingKinds,
                out kind))
        {
            title = block.Text;
            return true;
        }

        var firstSourceLine = GetFirstSourceLine(
            block.SourceText);
        if (firstSourceLine is not null &&
            !string.Equals(
                firstSourceLine,
                block.Text,
                StringComparison.Ordinal) &&
            TryResolveHeadingKindFromText(
                firstSourceLine,
                headingKinds,
                out kind))
        {
            title = firstSourceLine;
            return true;
        }

        kind = default;
        title = string.Empty;
        return false;
    }

    private static bool TryResolveHeadingKindFromText(
        string heading,
        IReadOnlyDictionary<string, DocumentSegmentKind> headingKinds,
        out DocumentSegmentKind kind)
    {
        var candidate = NormalizeHeadingKey(
            heading);

        if (headingKinds.TryGetValue(
                candidate,
                out kind))
        {
            return true;
        }

        foreach (var pair in headingKinds)
        {
            if (HasAllowedShortPrefix(
                    candidate,
                    pair.Key) ||
                string.Equals(
                    CompactHeadingKey(candidate),
                    CompactHeadingKey(pair.Key),
                    StringComparison.Ordinal))
            {
                kind = pair.Value;
                return true;
            }
        }

        kind = default;
        return false;
    }

    private static bool HasAllowedShortPrefix(
        string candidate,
        string expected)
    {
        if (!candidate.EndsWith(
                expected,
                StringComparison.Ordinal))
        {
            return false;
        }

        var prefixLength =
            candidate.Length -
            expected.Length;
        if (prefixLength <= 0)
        {
            return false;
        }

        var prefix = candidate[
            ..prefixLength].Trim();

        return prefix.Length is > 0 and <= 3 &&
               prefix.All(character =>
                   !char.IsLetter(character) ||
                   char.IsUpper(character));
    }

    private static string? GetFirstSourceLine(
        string sourceText)
    {
        var firstLine = sourceText
            .Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(firstLine)
            ? null
            : firstLine;
    }

    private static string CompactHeadingKey(
        string heading) =>
        new string(heading
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static bool HasMinimumHeadingTextQuality(
        string text)
    {
        var nonWhitespaceCount = text.Count(
            character =>
                !char.IsWhiteSpace(character));
        if (nonWhitespaceCount == 0)
        {
            return false;
        }

        var letterCount = text.Count(
            char.IsLetter);
        if (letterCount < 4)
        {
            return false;
        }

        var letterOrDigitCount = text.Count(
            char.IsLetterOrDigit);

        return letterOrDigitCount /
               (double)nonWhitespaceCount >=
               0.55;
    }

    private static bool IsHeadingCandidate(
        NormalizedPdfTextBlock block,
        double? bodyFontSize)
    {
        if (bodyFontSize is null or <= 0 ||
            block.MedianPointSize is null or <= 0 ||
            block.Text.Length > MaximumHeadingCharacters ||
            block.WordCount > MaximumHeadingWords)
        {
            return false;
        }

        var fontRatio =
            block.MedianPointSize.Value /
            bodyFontSize.Value;

        if (fontRatio < MinimumHeadingFontRatio)
        {
            return false;
        }

        if (fontRatio < SectionFontRatio &&
            LooksLikeSentence(block.Text))
        {
            return false;
        }

        return true;
    }

    private static DocumentSegmentType DetermineSegmentType(
        NormalizedPdfTextBlock block,
        double? bodyFontSize)
    {
        if (bodyFontSize is null or <= 0 ||
            block.MedianPointSize is null or <= 0)
        {
            return DocumentSegmentType.Section;
        }

        var ratio =
            block.MedianPointSize.Value /
            bodyFontSize.Value;

        if (ratio >= ChapterFontRatio)
        {
            return DocumentSegmentType.Chapter;
        }

        if (ratio >= SectionFontRatio)
        {
            return DocumentSegmentType.Section;
        }

        return DocumentSegmentType.Subsection;
    }

    private static bool LooksLikeSentence(
        string text)
    {
        var trimmed = text.TrimEnd();

        return trimmed.EndsWith('.') ||
               trimmed.EndsWith(';') ||
               trimmed.EndsWith(',');
    }

    private static double? GetWeightedMedianFontSize(
        IReadOnlyCollection<BlockContext> blocks)
    {
        var samples = blocks
            .Where(context =>
                context.Block.MedianPointSize is > 0 &&
                context.Block.WordCount > 0)
            .Select(context => new FontSample(
                context.Block.MedianPointSize!.Value,
                Math.Max(1, context.Block.WordCount)))
            .OrderBy(sample => sample.PointSize)
            .ToArray();

        if (samples.Length == 0)
        {
            return null;
        }

        var totalWeight = samples.Sum(
            sample => (long)sample.Weight);
        var medianPosition =
            (totalWeight + 1) / 2;

        long accumulatedWeight = 0;

        foreach (var sample in samples)
        {
            accumulatedWeight += sample.Weight;

            if (accumulatedWeight >= medianPosition)
            {
                return sample.PointSize;
            }
        }

        return samples[^1].PointSize;
    }

    private static string NormalizeHeadingKey(
        string heading)
    {
        var normalized = WhitespaceRegex
            .Replace(heading, " ")
            .Trim();

        var start = 0;
        while (start < normalized.Length &&
               !char.IsLetterOrDigit(
                   normalized[start]))
        {
            start++;
        }

        var end = normalized.Length - 1;
        while (end >= start &&
               !char.IsLetterOrDigit(
                   normalized[end]))
        {
            end--;
        }

        if (start > end)
        {
            return string.Empty;
        }

        return normalized[
                start..(end + 1)]
            .ToUpperInvariant();
    }

    private static void FlushCurrent(
        ICollection<DocumentSegmentDraft> segments,
        ref SegmentAccumulator? current)
    {
        if (current is null ||
            current.Blocks.Count == 0)
        {
            current = null;
            return;
        }

        var sourceBlocks = current.Blocks
            .Select(block => new DocumentBlockReference(
                block.PageNumber,
                block.Block.ReadingOrder))
            .ToArray();

        var text = string.Join(
            "\n\n",
            current.Blocks.Select(block =>
                block.Block.Text));

        segments.Add(
            new DocumentSegmentDraft(
                segments.Count,
                current.Type,
                current.Kind,
                current.Title,
                text,
                current.Blocks.Min(block =>
                    block.PageNumber),
                current.Blocks.Max(block =>
                    block.PageNumber),
                sourceBlocks));

        current = null;
    }

    private sealed class SegmentAccumulator(
        DocumentSegmentType type,
        DocumentSegmentKind kind,
        string? title)
    {
        public DocumentSegmentType Type { get; } = type;

        public DocumentSegmentKind Kind { get; } = kind;

        public string? Title { get; } = title;

        public List<BlockContext> Blocks { get; } = [];

        public void Add(
            BlockContext block) =>
            Blocks.Add(block);
    }

    private sealed record BlockContext(
        int PageNumber,
        NormalizedPdfTextBlock Block);

    private sealed record FontSample(
        double PointSize,
        int Weight);
}
