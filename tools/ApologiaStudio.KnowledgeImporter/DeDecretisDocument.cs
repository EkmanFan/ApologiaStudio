using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ApologiaStudio.KnowledgeImporter;

internal sealed record PreparedArtifact(
    string Sha256,
    byte[] Bytes);

internal sealed record PreparedSegment(
    Guid Id,
    int Number,
    int StartPdfPage,
    int EndPdfPage,
    int StartPrintedPage,
    int EndPrintedPage,
    string Text)
{
    public string Locator =>
        StartPrintedPage == EndPrintedPage
            ? $"§{Number}; NPNF p. {StartPrintedPage}"
            : $"§{Number}; NPNF pp. {StartPrintedPage}–{EndPrintedPage}";
}

internal sealed record PreparedDeDecretis(
    string SourcePath,
    string RawSha256,
    long RawByteLength,
    PreparedArtifact ParsedArtifact,
    PreparedArtifact NormalizedArtifact,
    IReadOnlyList<PreparedSegment> Segments);

internal static partial class DeDecretisDocument
{
    public const string ProfileId = "de-decretis-npnf2-04-v1";
    public const string ExpectedRawSha256 =
        "de5e95573b7910292b4b07c02b5cfd834fe63dd5daf4056e9a947c96cb81bc75";
    public const long ExpectedRawByteLength = 11_963_985;
    public const int ExpectedPdfPageCount = 1_479;
    public const int FirstPdfPage = 512;
    public const int LastPdfPage = 561;
    public const int PdfToPrintedPageOffset = 30;
    public const double MinimumFontSize = 10.0;
    public const double MinimumBaselineY = 100.0;
    public const double MaximumBaselineY = 700.0;
    public const string SourceUri = "https://ccel.org/ccel/schaff/npnf204.html";

    private const double LineTolerance = 1.25;
    private const double MinimumSyntheticSpaceGap = 0.8;

    public static PreparedDeDecretis Prepare(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            throw new KnowledgeImportException($"Source PDF was not found: {fullPath}");
        }

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new KnowledgeImportException("The source artifact must be a PDF file.");
        }

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length != ExpectedRawByteLength)
        {
            throw new KnowledgeImportException(
                $"Unexpected source size. Expected {ExpectedRawByteLength} bytes, " +
                $"found {fileInfo.Length} bytes.");
        }

        var rawSha256 = ComputeFileSha256(fullPath);
        if (!string.Equals(
                rawSha256,
                ExpectedRawSha256,
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Unexpected source SHA-256. Expected {ExpectedRawSha256}, found {rawSha256}.");
        }

        var sourceLines = new List<SourceLine>();

        using (var document = PdfDocument.Open(fullPath))
        {
            if (document.NumberOfPages != ExpectedPdfPageCount)
            {
                throw new KnowledgeImportException(
                    $"Unexpected PDF page count. Expected {ExpectedPdfPageCount}, " +
                    $"found {document.NumberOfPages}.");
            }

            for (var pageNumber = FirstPdfPage;
                 pageNumber <= LastPdfPage;
                 pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = document.GetPage(pageNumber);
                sourceLines.AddRange(ExtractLines(pageNumber, page));
            }
        }

        var segments = BuildSegments(sourceLines);
        ValidateSegments(segments);

        var parsedBytes = Utf8NoBom.GetBytes(BuildParsedArtifact(sourceLines));
        var normalizedBytes = Utf8NoBom.GetBytes(BuildNormalizedArtifact(segments));

        var finalRawSha256 = ComputeFileSha256(fullPath);
        if (!string.Equals(finalRawSha256, rawSha256, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "The source PDF changed while it was being parsed.");
        }

        return new PreparedDeDecretis(
            fullPath,
            rawSha256,
            fileInfo.Length,
            new PreparedArtifact(ComputeSha256(parsedBytes), parsedBytes),
            new PreparedArtifact(ComputeSha256(normalizedBytes), normalizedBytes),
            segments);
    }

    private static IReadOnlyList<SourceLine> ExtractLines(
        int pdfPageNumber,
        Page page)
    {
        var glyphs = new List<Glyph>();

        foreach (var letter in page.Letters)
        {
            if (string.IsNullOrEmpty(letter.Value))
            {
                continue;
            }

            var fontSize = Convert.ToDouble(letter.FontSize);
            var baselineY = Convert.ToDouble(letter.StartBaseLine.Y);

            if (fontSize < MinimumFontSize ||
                baselineY < MinimumBaselineY ||
                baselineY > MaximumBaselineY)
            {
                continue;
            }

            glyphs.Add(
                new Glyph(
                    letter.Value,
                    Convert.ToDouble(letter.StartBaseLine.X),
                    Convert.ToDouble(letter.EndBaseLine.X),
                    baselineY,
                    fontSize));
        }

        var ordered = glyphs
            .OrderByDescending(x => x.BaselineY)
            .ThenBy(x => x.StartX)
            .ToArray();

        var lines = new List<SourceLine>();
        var current = new List<Glyph>();
        double? currentBaseline = null;

        foreach (var glyph in ordered)
        {
            if (currentBaseline is null ||
                Math.Abs(glyph.BaselineY - currentBaseline.Value) <= LineTolerance)
            {
                current.Add(glyph);
                currentBaseline ??= glyph.BaselineY;
                continue;
            }

            AddLine(lines, pdfPageNumber, current);
            current.Clear();
            current.Add(glyph);
            currentBaseline = glyph.BaselineY;
        }

        AddLine(lines, pdfPageNumber, current);
        return lines;
    }

    private static void AddLine(
        ICollection<SourceLine> lines,
        int pdfPageNumber,
        IReadOnlyCollection<Glyph> glyphs)
    {
        if (glyphs.Count == 0)
        {
            return;
        }

        var ordered = glyphs.OrderBy(x => x.StartX).ToArray();
        var builder = new StringBuilder();
        Glyph? previous = null;

        foreach (var glyph in ordered)
        {
            if (previous is not null &&
                !EndsWithWhitespace(previous.Text) &&
                !StartsWithWhitespace(glyph.Text))
            {
                var gap = glyph.StartX - previous.EndX;
                var threshold = Math.Max(
                    MinimumSyntheticSpaceGap,
                    Math.Min(previous.FontSize, glyph.FontSize) * 0.08);

                if (gap > threshold)
                {
                    builder.Append(' ');
                }
            }

            builder.Append(glyph.Text);
            previous = glyph;
        }

        var text = NormalizeLine(builder.ToString());
        if (text.Length > 0)
        {
            lines.Add(new SourceLine(pdfPageNumber, text));
        }
    }

    private static IReadOnlyList<PreparedSegment> BuildSegments(
        IReadOnlyList<SourceLine> lines)
    {
        var segments = new List<PreparedSegment>(32);
        SegmentBuilder? current = null;
        var suppressEditorialChapterHeading = false;
        var expectedSection = 1;

        foreach (var line in lines)
        {
            if (line.Text.StartsWith("Chapter ", StringComparison.Ordinal))
            {
                suppressEditorialChapterHeading = true;
                continue;
            }

            var match = SectionStartRegex().Match(line.Text);
            if (match.Success)
            {
                var number = int.Parse(
                    match.Groups["number"].Value,
                    System.Globalization.CultureInfo.InvariantCulture);

                if (number != expectedSection)
                {
                    throw new KnowledgeImportException(
                        $"Expected De Decretis section {expectedSection}, found {number} " +
                        $"on PDF page {line.PdfPageNumber}.");
                }

                if (current is not null)
                {
                    segments.Add(current.Build());
                }

                current = new SegmentBuilder(number, line.PdfPageNumber);
                current.Append(match.Groups["text"].Value, line.PdfPageNumber);
                suppressEditorialChapterHeading = false;
                expectedSection++;
                continue;
            }

            if (current is null || suppressEditorialChapterHeading)
            {
                continue;
            }

            current.Append(line.Text, line.PdfPageNumber);
        }

        if (current is not null)
        {
            segments.Add(current.Build());
        }

        return segments;
    }

    private static void ValidateSegments(IReadOnlyList<PreparedSegment> segments)
    {
        if (segments.Count != 32)
        {
            throw new KnowledgeImportException(
                $"Expected 32 De Decretis sections, found {segments.Count}.");
        }

        for (var index = 0; index < segments.Count; index++)
        {
            var expectedNumber = index + 1;
            if (segments[index].Number != expectedNumber)
            {
                throw new KnowledgeImportException(
                    $"Section sequence is invalid at index {index}: " +
                    $"expected {expectedNumber}, found {segments[index].Number}.");
            }

            if (segments[index].Text.Length < 100)
            {
                throw new KnowledgeImportException(
                    $"Section {expectedNumber} is unexpectedly short.");
            }
        }

        if (!segments[0].Text.Contains(
                "Thou hast done well",
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "Section 1 sentinel text was not reconstructed correctly.");
        }

        if (!segments[^1].Text.Contains(
                "endless ages of ages. Amen.",
                StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "Section 32 ending sentinel was not found.");
        }

        var allText = string.Join('\n', segments.Select(x => x.Text));
        if (allText.Contains(
                "Introduction to the de Sententia Dionysii",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new KnowledgeImportException(
                "Extraction crossed into the following work, De Sententia Dionysii.");
        }

        if (allText.Contains("Socr. Hist. ii. 43", StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                "Editorial footnote text leaked into the normalized primary-source text.");
        }
    }

    private static string BuildParsedArtifact(IReadOnlyList<SourceLine> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"profile: {ProfileId}");
        builder.AppendLine($"source-sha256: {ExpectedRawSha256}");
        builder.AppendLine($"pdf-pages: {FirstPdfPage}-{LastPdfPage}");
        builder.AppendLine("content: main-text-layer-without-running-headers-footnotes-or-page-numbers");

        var currentPage = 0;
        foreach (var line in lines)
        {
            if (line.PdfPageNumber != currentPage)
            {
                currentPage = line.PdfPageNumber;
                builder.AppendLine();
                builder.AppendLine(
                    $"[PDF_PAGE {currentPage}; PRINT_PAGE " +
                    $"{ToPrintedPage(currentPage)}]");
            }

            builder.AppendLine(line.Text);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string BuildNormalizedArtifact(
        IReadOnlyList<PreparedSegment> segments)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"profile: {ProfileId}");
        builder.AppendLine($"source-sha256: {ExpectedRawSha256}");
        builder.AppendLine("normalization: section-v1");
        builder.AppendLine();

        foreach (var segment in segments)
        {
            builder.AppendLine(segment.Locator);
            builder.AppendLine(segment.Text);
            builder.AppendLine();
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeLine(string value)
    {
        var normalized = value
            .Replace('\u0000', ' ')
            .Normalize(NormalizationForm.FormC);

        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
        normalized = SpaceBeforePunctuationRegex().Replace(normalized, "$1");
        normalized = SpaceAfterOpeningPunctuationRegex().Replace(normalized, "$1");
        return normalized;
    }

    private static bool EndsWithWhitespace(string value) =>
        value.Length > 0 && char.IsWhiteSpace(value[^1]);

    private static bool StartsWithWhitespace(string value) =>
        value.Length > 0 && char.IsWhiteSpace(value[0]);

    private static int ToPrintedPage(int pdfPageNumber) =>
        pdfPageNumber - PdfToPrintedPageOffset;

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    internal static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record Glyph(
        string Text,
        double StartX,
        double EndX,
        double BaselineY,
        double FontSize);

    private sealed record SourceLine(
        int PdfPageNumber,
        string Text);

    private sealed class SegmentBuilder
    {
        private readonly int _number;
        private readonly int _startPdfPage;
        private readonly StringBuilder _text = new();
        private int _endPdfPage;

        public SegmentBuilder(int number, int startPdfPage)
        {
            _number = number;
            _startPdfPage = startPdfPage;
            _endPdfPage = startPdfPage;
        }

        public void Append(string value, int pdfPage)
        {
            var text = value.Trim();
            if (text.Length == 0)
            {
                return;
            }

            if (_text.Length > 0 && _text[^1] != '-')
            {
                _text.Append(' ');
            }

            _text.Append(text);
            _endPdfPage = pdfPage;
        }

        public PreparedSegment Build()
        {
            var text = WhitespaceRegex()
                .Replace(_text.ToString(), " ")
                .Trim();
            text = SpaceBeforePunctuationRegex().Replace(text, "$1");
            text = SpaceAfterOpeningPunctuationRegex().Replace(text, "$1");

            return new PreparedSegment(
                StableKnowledgeIds.ForProfile($"segment:{_number}"),
                _number,
                _startPdfPage,
                _endPdfPage,
                ToPrintedPage(_startPdfPage),
                ToPrintedPage(_endPdfPage),
                text);
        }
    }

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    [GeneratedRegex(@"^(?<number>\d{1,2})\.\s*(?<text>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex SectionStartRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+([,.;:!?\)\]\}”’])", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuationRegex();

    [GeneratedRegex(@"([\(\[\{“‘])\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceAfterOpeningPunctuationRegex();
}
