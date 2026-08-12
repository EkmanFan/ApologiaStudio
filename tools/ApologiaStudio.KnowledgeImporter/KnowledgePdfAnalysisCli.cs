using System.Globalization;
using System.Text;
using System.Text.Json;
using ApologiaStudio.Application.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Knowledge.Ingestion;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgePdfAnalysisCli
{
    private const string ReportSchemaVersion =
        "apologia-pdf-real-document-analysis-v2";

    private const int DefaultMaxSamples = 12;
    private const int MaximumProbeSamples = 5;
    private const int MaximumHeadingSamples = 40;
    private const int MaximumBlockSamplesPerPage = 16;
    private const int MaximumSnippetCharacters = 180;
    private const int MaximumTextlessPageSamples = 100;
    private const double DominantRasterImageAreaRatio = 0.60;

    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 ||
            args.Contains("--help", StringComparer.Ordinal))
        {
            WriteUsage();
            return args.Length == 0 ? 2 : 0;
        }

        var options = AnalysisOptions.Parse(args);

        var extractor = new PdfPigDocumentExtractor();
        var extracted = await extractor.ExtractAsync(
            options.SourcePath,
            cancellationToken);

        var selected = SelectPages(
            extracted,
            options.FirstPage,
            options.LastPage);

        var normalizer = new PdfDocumentNormalizer();
        var normalized = normalizer.Normalize(
            selected,
            cancellationToken);

        var segmenter = new HeuristicDocumentSegmenter();
        var segmentation = segmenter.Segment(
            normalized,
            new DocumentSegmentationHints(
                options.KindHeadingHints),
            cancellationToken);

        var report = BuildReport(
            extracted,
            selected,
            normalized,
            segmentation,
            options,
            cancellationToken);

        await WriteReportAsync(
            options.ReportPath,
            report,
            cancellationToken);

        WriteSummary(
            report,
            options.ReportPath);

        return 0;
    }

    private static ExtractedPdfDocument SelectPages(
        ExtractedPdfDocument document,
        int? firstPage,
        int? lastPage)
    {
        if (firstPage is null &&
            lastPage is null)
        {
            return document;
        }

        if (firstPage is null ||
            lastPage is null)
        {
            throw new KnowledgeImportException(
                "Both first and last page must be specified.");
        }

        if (firstPage.Value < 1 ||
            lastPage.Value < firstPage.Value ||
            lastPage.Value > document.PageCount)
        {
            throw new KnowledgeImportException(
                $"Invalid PDF page range {firstPage}-{lastPage}. " +
                $"The document contains {document.PageCount} pages.");
        }

        var pages = document.Pages
            .Where(page =>
                page.PageNumber >= firstPage.Value &&
                page.PageNumber <= lastPage.Value)
            .ToArray();

        return document with
        {
            PageCount = pages.Length,
            Pages = pages
        };
    }

    private static PdfRealDocumentAnalysisReport BuildReport(
        ExtractedPdfDocument fullDocument,
        ExtractedPdfDocument selectedDocument,
        NormalizedPdfDocument normalized,
        DocumentSegmentationResult segmentation,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var selectedPages = selectedDocument.Pages;
        var normalizedBlocks = normalized.Pages
            .SelectMany(page => page.Blocks)
            .ToArray();

        var layoutDiagnostics = BuildLayoutDiagnostics(
            normalized,
            options.MaxSamples,
            cancellationToken);

        var probeDiagnostics = options.Probes
            .Select(probe => BuildProbeDiagnostic(
                probe,
                selectedDocument,
                normalized,
                segmentation))
            .ToArray();

        var headingSamples = segmentation.Segments
            .Where(segment =>
                !string.IsNullOrWhiteSpace(segment.Title))
            .Take(MaximumHeadingSamples)
            .Select(segment => new PdfHeadingSample(
                segment.StartPage,
                segment.EndPage,
                segment.Type.ToString(),
                segment.Kind.ToString(),
                TruncateAndNormalize(segment.Title!)))
            .ToArray();

        var segmentLengths = segmentation.Segments
            .Select(segment => segment.Text.Length)
            .OrderBy(length => length)
            .ToArray();

        var pagesWithWords = selectedPages.Count(page =>
            page.Words.Count > 0);
        var pagesWithoutWords = selectedPages.Count -
            pagesWithWords;
        var textlessRasterPages = selectedPages
            .Where(page =>
                page.Words.Count == 0 &&
                page.LargestRasterImageAreaRatio >=
                DominantRasterImageAreaRatio)
            .Select(page => page.PageNumber)
            .OrderBy(pageNumber => pageNumber)
            .ToArray();

        var extractionMetrics = new PdfExtractionMetrics(
            selectedPages.Sum(page => page.Words.Count),
            selectedPages.Sum(page => page.Blocks.Count),
            pagesWithWords,
            pagesWithoutWords,
            selectedPages.Count(page =>
                page.Blocks.Count == 0),
            selectedPages.Count == 0
                ? 0
                : Math.Round(
                    pagesWithWords * 100.0 /
                    selectedPages.Count,
                    1),
            DominantRasterImageAreaRatio,
            textlessRasterPages.Length,
            selectedPages
                .Where(page =>
                    page.Words.Count == 0)
                .Select(page => page.PageNumber)
                .OrderBy(pageNumber => pageNumber)
                .Take(MaximumTextlessPageSamples)
                .ToArray(),
            textlessRasterPages
                .Take(MaximumTextlessPageSamples)
                .ToArray());

        var normalizationMetrics = new PdfNormalizationMetrics(
            normalizedBlocks.Length,
            normalizedBlocks.Count(block =>
                !block.IsExcluded),
            normalizedBlocks.Count(block =>
                block.IsExcluded &&
                block.ExclusionReason ==
                PdfBlockExclusionReason.RepeatedHeader),
            normalizedBlocks.Count(block =>
                block.IsExcluded &&
                block.ExclusionReason ==
                PdfBlockExclusionReason.RepeatedFooter));

        var segmentationMetrics = new PdfSegmentationMetrics(
            segmentation.Segments.Count,
            CountByName(segmentation.Segments.Select(
                segment => segment.Type.ToString())),
            CountByName(segmentation.Segments.Select(
                segment => segment.Kind.ToString())),
            segmentLengths.Length == 0
                ? 0
                : segmentLengths[0],
            GetMedian(segmentLengths),
            segmentLengths.Length == 0
                ? 0
                : segmentLengths[^1],
            segmentLengths.Count(length =>
                length <= 80),
            segmentLengths.Count(length =>
                length >= 10_000));

        var selectedFirstPage = selectedPages.Count == 0
            ? 0
            : selectedPages.Min(page => page.PageNumber);
        var selectedLastPage = selectedPages.Count == 0
            ? 0
            : selectedPages.Max(page => page.PageNumber);

        return new PdfRealDocumentAnalysisReport(
            ReportSchemaVersion,
            options.Label,
            DateTimeOffset.UtcNow,
            fullDocument.SourceFileName,
            fullDocument.SourceSha256,
            fullDocument.SourceByteLength,
            fullDocument.PageCount,
            new PdfPageSelection(
                selectedFirstPage,
                selectedLastPage,
                selectedDocument.PageCount),
            selectedDocument.ExtractionProfileId,
            normalized.NormalizationProfileId,
            segmentation.SegmentationProfileId,
            extractionMetrics,
            normalizationMetrics,
            segmentationMetrics,
            layoutDiagnostics,
            probeDiagnostics,
            headingSamples);
    }

    private static PdfLayoutDiagnostics BuildLayoutDiagnostics(
        NormalizedPdfDocument document,
        int maxSamples,
        CancellationToken cancellationToken)
    {
        var pages = new List<PdfPageLayoutDiagnostic>();

        foreach (var page in document.Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blocks = page.Blocks
                .Where(block =>
                    !block.IsExcluded &&
                    !string.IsNullOrWhiteSpace(block.Text))
                .OrderBy(block => block.ReadingOrder)
                .ToArray();

            var classifications = blocks
                .Select(block => new ClassifiedBlock(
                    block,
                    ClassifyColumn(
                        page.Width,
                        block)))
                .ToArray();

            var leftCount = classifications.Count(item =>
                item.Column == "L");
            var rightCount = classifications.Count(item =>
                item.Column == "R");
            var wideCount = classifications.Count(item =>
                item.Column == "W");

            var multiColumnCandidate =
                leftCount >= 2 &&
                rightCount >= 2;

            if (!multiColumnCandidate)
            {
                continue;
            }

            var narrowColumns = classifications
                .Where(item =>
                    item.Column is "L" or "R")
                .ToArray();

            var sequence = string.Concat(
                narrowColumns.Select(item =>
                    item.Column));

            var switchCount = CountColumnSwitches(
                narrowColumns);

            var verticalReversals =
                CountVerticalReversals(
                    page.Height,
                    narrowColumns,
                    "L") +
                CountVerticalReversals(
                    page.Height,
                    narrowColumns,
                    "R");

            var interleavedColumns =
                switchCount > 1;
            var hasVerticalReversal =
                verticalReversals > 0;

            pages.Add(
                new PdfPageLayoutDiagnostic(
                    page.PageNumber,
                    leftCount,
                    rightCount,
                    wideCount,
                    switchCount,
                    verticalReversals,
                    interleavedColumns,
                    hasVerticalReversal,
                    sequence.Length <= 100
                        ? sequence
                        : sequence[..100] + "...",
                    classifications
                        .Take(MaximumBlockSamplesPerPage)
                        .Select(item =>
                            new PdfBlockLayoutSample(
                                item.Block.ReadingOrder,
                                item.Column,
                                Math.Round(
                                    item.Block.BoundingBox.Left,
                                    1),
                                Math.Round(
                                    item.Block.BoundingBox.Top,
                                    1),
                                Math.Round(
                                    item.Block.BoundingBox.Width,
                                    1),
                                TruncateAndNormalize(
                                    item.Block.Text)))
                        .ToArray()));
        }

        var samples = pages
            .OrderByDescending(page =>
                page.HasVerticalReversal)
            .ThenByDescending(page =>
                page.VerticalReversalCount)
            .ThenByDescending(page =>
                page.ColumnSwitchCount)
            .ThenBy(page =>
                page.PageNumber)
            .Take(maxSamples)
            .ToArray();

        return new PdfLayoutDiagnostics(
            pages.Count,
            pages.Count(page =>
                page.InterleavedColumns),
            pages.Count(page =>
                page.HasVerticalReversal),
            samples);
    }

    private static PdfProbeDiagnostic BuildProbeDiagnostic(
        string probe,
        ExtractedPdfDocument extractedDocument,
        NormalizedPdfDocument document,
        DocumentSegmentationResult segmentation)
    {
        var wordStreamMatches = extractedDocument.Pages
            .Count(page =>
                string.Join(
                        ' ',
                        page.Words
                            .OrderBy(word => word.Ordinal)
                            .Select(word => word.Text))
                    .Contains(
                        probe,
                        StringComparison.OrdinalIgnoreCase));

        var matches = document.Pages
            .SelectMany(page =>
                page.Blocks
                    .Where(block =>
                        !block.IsExcluded &&
                        block.Text.Contains(
                            probe,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(block => new
                    {
                        page.PageNumber,
                        block.Text
                    }))
            .ToArray();

        var segmentTitleMatches = segmentation.Segments
            .Where(segment =>
                segment.Title is not null &&
                segment.Title.Contains(
                    probe,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new PdfProbeDiagnostic(
            probe,
            wordStreamMatches,
            matches.Length,
            segmentTitleMatches.Length,
            matches
                .Select(match =>
                    match.PageNumber)
                .Distinct()
                .OrderBy(page =>
                    page)
                .Take(100)
                .ToArray(),
            matches
                .Take(MaximumProbeSamples)
                .Select(match =>
                    new PdfProbeSample(
                        match.PageNumber,
                        BuildProbeSnippet(
                            match.Text,
                            probe)))
                .ToArray());
    }

    private static string ClassifyColumn(
        double pageWidth,
        NormalizedPdfTextBlock block)
    {
        if (pageWidth <= 0)
        {
            return "W";
        }

        if (block.BoundingBox.Width >=
            pageWidth * 0.55)
        {
            return "W";
        }

        var center =
            (block.BoundingBox.Left +
             block.BoundingBox.Right) / 2.0;

        return center < pageWidth / 2.0
            ? "L"
            : "R";
    }

    private static int CountColumnSwitches(
        IReadOnlyList<ClassifiedBlock> blocks)
    {
        if (blocks.Count <= 1)
        {
            return 0;
        }

        var switches = 0;
        var previous = blocks[0].Column;

        for (var index = 1;
             index < blocks.Count;
             index++)
        {
            if (blocks[index].Column ==
                previous)
            {
                continue;
            }

            switches++;
            previous = blocks[index].Column;
        }

        return switches;
    }

    private static int CountVerticalReversals(
        double pageHeight,
        IReadOnlyCollection<ClassifiedBlock> blocks,
        string column)
    {
        var tolerance =
            Math.Max(2.0, pageHeight * 0.03);

        double? previousTop = null;
        var reversals = 0;

        foreach (var item in blocks.Where(item =>
                     item.Column == column))
        {
            if (previousTop is not null &&
                item.Block.BoundingBox.Top >
                previousTop.Value + tolerance)
            {
                reversals++;
            }

            previousTop =
                item.Block.BoundingBox.Top;
        }

        return reversals;
    }

    private static IReadOnlyDictionary<string, int>
        CountByName(
            IEnumerable<string> values) =>
        values
            .GroupBy(
                value => value,
                StringComparer.Ordinal)
            .OrderBy(group =>
                group.Key,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);

    private static int GetMedian(
        IReadOnlyList<int> orderedValues)
    {
        if (orderedValues.Count == 0)
        {
            return 0;
        }

        var middle =
            orderedValues.Count / 2;

        if (orderedValues.Count % 2 != 0)
        {
            return orderedValues[middle];
        }

        return (int)Math.Round(
            (orderedValues[middle - 1] +
             orderedValues[middle]) / 2.0,
            MidpointRounding.AwayFromZero);
    }

    private static string BuildProbeSnippet(
        string text,
        string probe)
    {
        var normalized =
            NormalizeSingleLine(text);
        var index = normalized.IndexOf(
            probe,
            StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return TruncateAndNormalize(
                normalized);
        }

        var start = Math.Max(
            0,
            index - 60);
        var remaining =
            normalized.Length - start;
        var length = Math.Min(
            MaximumSnippetCharacters,
            remaining);

        var snippet = normalized.Substring(
            start,
            length);

        if (start > 0)
        {
            snippet = "..." + snippet;
        }

        if (start + length <
            normalized.Length)
        {
            snippet += "...";
        }

        return snippet;
    }

    private static string TruncateAndNormalize(
        string text)
    {
        var normalized =
            NormalizeSingleLine(text);

        return normalized.Length <=
               MaximumSnippetCharacters
            ? normalized
            : normalized[
                ..MaximumSnippetCharacters] + "...";
    }

    private static string NormalizeSingleLine(
        string text) =>
        string.Join(
            ' ',
            text.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    private static async Task WriteReportAsync(
        string reportPath,
        PdfRealDocumentAnalysisReport report,
        CancellationToken cancellationToken)
    {
        var fullPath =
            Path.GetFullPath(reportPath);
        var directory =
            Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(
            report,
            options);

        var temporaryPath =
            fullPath + ".tmp-" +
            Guid.NewGuid().ToString("N");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            File.Move(
                temporaryPath,
                fullPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }
        }
    }

    private static void WriteSummary(
        PdfRealDocumentAnalysisReport report,
        string reportPath)
    {
        Console.WriteLine(
            "RESULT: ANALYZED");
        Console.WriteLine(
            $"Label: {report.Label}");
        Console.WriteLine(
            $"Source: {report.SourceFileName}");
        Console.WriteLine(
            $"Source SHA-256: {report.SourceSha256}");
        Console.WriteLine(
            $"Source bytes: {report.SourceByteLength}");
        Console.WriteLine(
            $"PDF pages total: {report.TotalPdfPages}");
        Console.WriteLine(
            $"PDF pages selected: " +
            $"{report.PageSelection.FirstPage}-" +
            $"{report.PageSelection.LastPage} " +
            $"({report.PageSelection.PageCount})");
        Console.WriteLine(
            $"Words: {report.Extraction.WordCount}");
        Console.WriteLine(
            $"Blocks: {report.Extraction.BlockCount}");
        Console.WriteLine(
            $"Text-layer coverage: " +
            $"{report.Extraction.TextLayerCoveragePercent:F1}% " +
            $"({report.Extraction.PagesWithWords}/" +
            $"{report.PageSelection.PageCount} selected pages)");
        Console.WriteLine(
            $"Textless pages with dominant raster image: " +
            $"{report.Extraction.TextlessPagesWithDominantRasterImage}");
        if (report.Extraction.PagesWithoutWords > 0)
        {
            Console.WriteLine(
                "WARNING: selected pages without extractable text were " +
                "detected; complete text ingestion is not established.");
        }
        Console.WriteLine(
            $"Excluded recurring headers: " +
            $"{report.Normalization.ExcludedHeaderBlocks}");
        Console.WriteLine(
            $"Excluded recurring footers: " +
            $"{report.Normalization.ExcludedFooterBlocks}");
        Console.WriteLine(
            $"Segments: {report.Segmentation.SegmentCount}");
        Console.WriteLine(
            $"Multi-column candidate pages: " +
            $"{report.Layout.MultiColumnCandidatePages}");
        Console.WriteLine(
            $"Interleaved multi-column pages: " +
            $"{report.Layout.InterleavedColumnPages}");
        Console.WriteLine(
            $"Vertical reading-order reversal pages: " +
            $"{report.Layout.VerticalReversalPages}");

        foreach (var probe in report.Probes)
        {
            Console.WriteLine(
                $"Probe '{probe.Probe}': " +
                $"{probe.WordStreamMatches} page-word-stream match(es), " +
                $"{probe.BlockMatches} block match(es), " +
                $"{probe.SegmentTitleMatches} segment-title match(es)");
        }

        Console.WriteLine(
            $"Report: {Path.GetFullPath(reportPath)}");
    }

    private static void WriteUsage()
    {
        Console.WriteLine(
            """
            Analyze a born-digital PDF through the generic extraction,
            normalization, and segmentation pipeline without importing it.

            This command is diagnostic only. It does not write to the
            Knowledge Store and does not materialize managed artifacts.

            Usage:
              dotnet run --project tools/ApologiaStudio.KnowledgeImporter -- \
                analyze-pdf \
                --source /absolute/path/document.pdf \
                --report /absolute/path/report.json \
                [--label "document label"] \
                [--pages 10-40] \
                [--probe "text to locate"] \
                [--kind-heading "HEADING=pedagogical_prompt"] \
                [--max-samples 12]

            --pages limits normalization and segmentation to the selected
            PDF page range. Source identity and total page count still
            describe the complete PDF.

            --probe may be repeated.

            --kind-heading may be repeated. Supported values:
              unknown
              main_text
              pedagogical_prompt
              sidebar
              bibliography
              caption
              glossary
              index

            The JSON report intentionally contains metrics and bounded text
            samples rather than a copy of the source document.
            """);
    }

    private sealed record AnalysisOptions(
        string SourcePath,
        string ReportPath,
        string Label,
        int? FirstPage,
        int? LastPage,
        IReadOnlyList<string> Probes,
        IReadOnlyList<HeadingSegmentKindHint>
            KindHeadingHints,
        int MaxSamples)
    {
        public static AnalysisOptions Parse(
            IReadOnlyList<string> args)
        {
            string? sourcePath = null;
            string? reportPath = null;
            string? label = null;
            int? firstPage = null;
            int? lastPage = null;
            var probes = new List<string>();
            var hints =
                new List<HeadingSegmentKindHint>();
            var maxSamples =
                DefaultMaxSamples;

            for (var index = 0;
                 index < args.Count;
                 index++)
            {
                switch (args[index])
                {
                    case "--source":
                        sourcePath = ReadValue(
                            args,
                            ref index,
                            "--source");
                        break;

                    case "--report":
                        reportPath = ReadValue(
                            args,
                            ref index,
                            "--report");
                        break;

                    case "--label":
                        label = ReadValue(
                            args,
                            ref index,
                            "--label");
                        break;

                    case "--pages":
                    {
                        var value = ReadValue(
                            args,
                            ref index,
                            "--pages");
                        (firstPage, lastPage) =
                            ParsePageRange(
                                value);
                        break;
                    }

                    case "--probe":
                        probes.Add(
                            ReadValue(
                                args,
                                ref index,
                                "--probe"));
                        break;

                    case "--kind-heading":
                        hints.Add(
                            ParseHeadingHint(
                                ReadValue(
                                    args,
                                    ref index,
                                    "--kind-heading")));
                        break;

                    case "--max-samples":
                    {
                        var value = ReadValue(
                            args,
                            ref index,
                            "--max-samples");

                        if (!int.TryParse(
                                value,
                                NumberStyles.None,
                                CultureInfo.InvariantCulture,
                                out maxSamples) ||
                            maxSamples is < 1 or > 50)
                        {
                            throw new KnowledgeImportException(
                                "--max-samples must be an integer from 1 to 50.");
                        }

                        break;
                    }

                    default:
                        throw new KnowledgeImportException(
                            $"Unknown analyze-pdf option: {args[index]}");
                }
            }

            if (string.IsNullOrWhiteSpace(
                    sourcePath))
            {
                throw new KnowledgeImportException(
                    "Missing required analyze-pdf option --source.");
            }

            var fullSourcePath =
                Path.GetFullPath(sourcePath);

            reportPath ??=
                Path.Combine(
                    Environment.CurrentDirectory,
                    Path.GetFileNameWithoutExtension(
                        fullSourcePath) +
                    "-pdf-analysis.json");

            label ??=
                Path.GetFileName(
                    fullSourcePath);

            return new AnalysisOptions(
                fullSourcePath,
                Path.GetFullPath(reportPath),
                label,
                firstPage,
                lastPage,
                probes,
                hints,
                maxSamples);
        }

        private static (
            int FirstPage,
            int LastPage)
            ParsePageRange(
                string value)
        {
            var separator =
                value.IndexOf(
                    '-');

            if (separator <= 0 ||
                separator ==
                value.Length - 1)
            {
                throw new KnowledgeImportException(
                    "--pages must use the form FIRST-LAST.");
            }

            if (!int.TryParse(
                    value[..separator],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var firstPage) ||
                !int.TryParse(
                    value[(separator + 1)..],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var lastPage) ||
                firstPage < 1 ||
                lastPage < firstPage)
            {
                throw new KnowledgeImportException(
                    "--pages contains an invalid page range.");
            }

            return (
                firstPage,
                lastPage);
        }

        private static HeadingSegmentKindHint
            ParseHeadingHint(
                string value)
        {
            var separator =
                value.LastIndexOf(
                    '=');

            if (separator <= 0 ||
                separator ==
                value.Length - 1)
            {
                throw new KnowledgeImportException(
                    "--kind-heading must use the form HEADING=segment_kind.");
            }

            var heading =
                value[..separator].Trim();
            var kindText =
                value[(separator + 1)..]
                    .Trim()
                    .Replace(
                        '-',
                        '_')
                    .ToLowerInvariant();

            if (heading.Length == 0)
            {
                throw new KnowledgeImportException(
                    "--kind-heading cannot contain an empty heading.");
            }

            var kind = kindText switch
            {
                "unknown" =>
                    DocumentSegmentKind.Unknown,
                "main_text" =>
                    DocumentSegmentKind.MainText,
                "pedagogical_prompt" =>
                    DocumentSegmentKind.PedagogicalPrompt,
                "sidebar" =>
                    DocumentSegmentKind.Sidebar,
                "bibliography" =>
                    DocumentSegmentKind.Bibliography,
                "caption" =>
                    DocumentSegmentKind.Caption,
                "glossary" =>
                    DocumentSegmentKind.Glossary,
                "index" =>
                    DocumentSegmentKind.Index,
                _ => throw new KnowledgeImportException(
                    $"Unknown segment kind '{kindText}'.")
            };

            return new HeadingSegmentKindHint(
                heading,
                kind);
        }

        private static string ReadValue(
            IReadOnlyList<string> args,
            ref int index,
            string option)
        {
            index++;

            if (index >= args.Count ||
                args[index].StartsWith(
                    "--",
                    StringComparison.Ordinal))
            {
                throw new KnowledgeImportException(
                    $"Missing value for {option}.");
            }

            return args[index];
        }
    }

    private sealed record ClassifiedBlock(
        NormalizedPdfTextBlock Block,
        string Column);

    private sealed record PdfRealDocumentAnalysisReport(
        string SchemaVersion,
        string Label,
        DateTimeOffset GeneratedAtUtc,
        string SourceFileName,
        string SourceSha256,
        long SourceByteLength,
        int TotalPdfPages,
        PdfPageSelection PageSelection,
        string ExtractionProfileId,
        string NormalizationProfileId,
        string SegmentationProfileId,
        PdfExtractionMetrics Extraction,
        PdfNormalizationMetrics Normalization,
        PdfSegmentationMetrics Segmentation,
        PdfLayoutDiagnostics Layout,
        IReadOnlyList<PdfProbeDiagnostic> Probes,
        IReadOnlyList<PdfHeadingSample> HeadingSamples);

    private sealed record PdfPageSelection(
        int FirstPage,
        int LastPage,
        int PageCount);

    private sealed record PdfExtractionMetrics(
        int WordCount,
        int BlockCount,
        int PagesWithWords,
        int PagesWithoutWords,
        int PagesWithoutBlocks,
        double TextLayerCoveragePercent,
        double DominantRasterImageAreaThreshold,
        int TextlessPagesWithDominantRasterImage,
        IReadOnlyList<int> TextlessPageSamples,
        IReadOnlyList<int> TextlessDominantRasterPageSamples);

    private sealed record PdfNormalizationMetrics(
        int BlockCount,
        int IncludedBlocks,
        int ExcludedHeaderBlocks,
        int ExcludedFooterBlocks);

    private sealed record PdfSegmentationMetrics(
        int SegmentCount,
        IReadOnlyDictionary<string, int>
            SegmentTypes,
        IReadOnlyDictionary<string, int>
            SegmentKinds,
        int MinimumCharacters,
        int MedianCharacters,
        int MaximumCharacters,
        int SegmentsAtMost80Characters,
        int SegmentsAtLeast10000Characters);

    private sealed record PdfLayoutDiagnostics(
        int MultiColumnCandidatePages,
        int InterleavedColumnPages,
        int VerticalReversalPages,
        IReadOnlyList<PdfPageLayoutDiagnostic>
            Samples);

    private sealed record PdfPageLayoutDiagnostic(
        int PageNumber,
        int LeftBlocks,
        int RightBlocks,
        int WideBlocks,
        int ColumnSwitchCount,
        int VerticalReversalCount,
        bool InterleavedColumns,
        bool HasVerticalReversal,
        string NarrowColumnSequence,
        IReadOnlyList<PdfBlockLayoutSample>
            Blocks);

    private sealed record PdfBlockLayoutSample(
        int ReadingOrder,
        string Column,
        double Left,
        double Top,
        double Width,
        string Text);

    private sealed record PdfProbeDiagnostic(
        string Probe,
        int WordStreamMatches,
        int BlockMatches,
        int SegmentTitleMatches,
        IReadOnlyList<int> Pages,
        IReadOnlyList<PdfProbeSample> Samples);

    private sealed record PdfProbeSample(
        int PageNumber,
        string Text);

    private sealed record PdfHeadingSample(
        int StartPage,
        int EndPage,
        string Type,
        string Kind,
        string Title);
}
