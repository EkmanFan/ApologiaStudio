using System.Text.Json;

namespace ApologiaStudio.BibleCorpusBench;

public static class BenchmarkCli
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
        {
            WriteUsage();
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            var options = BenchmarkOptions.Parse(args);
            var usfm = new UsfmCorpusReader().Read(options.UsfmPath);
            var vpl = new VplCorpusReader().Read(options.VplPath);
            var report = new CorpusComparer().Compare(
                options.Name,
                usfm,
                vpl,
                options.ExpectedBookCount,
                options.RequireStrongAttributes,
                options.MaxDifferenceSamples);

            WriteSummary(report);
            if (options.ReportPath is not null)
            {
                WriteReport(options.ReportPath, report);
            }

            return report.IsMatch ? 0 : 1;
        }
        catch (BibleCorpusException exception)
        {
            Console.Error.WriteLine($"Corpus validation failed: {exception.Message}");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected failure: {exception}");
            return 2;
        }
    }

    private static void WriteSummary(CorpusValidationReport report)
    {
        Console.WriteLine($"Corpus: {report.CorpusName}");
        Console.WriteLine($"USFM: {report.UsfmBookCount} books, {report.UsfmVerseCount} verses, {report.UsfmFileCount} files");
        Console.WriteLine($"VPL:  {report.VplBookCount} books, {report.VplVerseCount} verses, {report.VplFileCount} files");
        Console.WriteLine($"Strong attributes: {report.StrongAttributeCount}");
        Console.WriteLine($"Missing from USFM: {report.MissingFromUsfmCount}");
        Console.WriteLine($"Unexpected in USFM: {report.UnexpectedInUsfmCount}");
        Console.WriteLine($"Text mismatches: {report.TextMismatchCount}");
        Console.WriteLine(report.IsMatch ? "RESULT: MATCH" : "RESULT: DIFFERENCES FOUND");

        foreach (var difference in report.Differences)
        {
            Console.WriteLine($"- {difference.Reference}");
            if (difference.UsfmText is not null)
            {
                Console.WriteLine($"  USFM: {difference.UsfmText}");
            }

            if (difference.VplText is not null)
            {
                Console.WriteLine($"  VPL:  {difference.VplText}");
            }
        }
    }

    private static void WriteReport(string path, CorpusValidationReport report)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(fullPath, json);
        Console.WriteLine($"Report: {fullPath}");
    }

    private static void WriteUsage()
    {
        Console.WriteLine(
            """
            Validate one USFM corpus against its VPL oracle.

            Usage:
              dotnet run --project tools/ApologiaStudio.BibleCorpusBench -- \
                --name LSG1910 \
                --usfm /absolute/path/to/usfm \
                --vpl /absolute/path/to/corpus.vpl \
                --expected-books 66 \
                --require-strong \
                --report artifacts/bible-corpus-validation/lsg1910.json

            Options:
              --name <value>              Corpus label used in the report.
              --usfm <path>               USFM file or directory (.usfm/.sfm, recursive).
              --vpl <path>                VPL file or directory (.vpl/.txt, recursive).
              --expected-books <number>   Expected books in both inputs. Default: 66.
              --require-strong            Fail when no USFM strong attribute is found.
              --max-samples <number>      Maximum differences written to the report. Default: 20.
              --report <path>             Optional JSON report path.
              --help                      Show this help.
            """);
    }

    private sealed record BenchmarkOptions(
        string Name,
        string UsfmPath,
        string VplPath,
        int ExpectedBookCount,
        bool RequireStrongAttributes,
        int MaxDifferenceSamples,
        string? ReportPath)
    {
        public static BenchmarkOptions Parse(IReadOnlyList<string> args)
        {
            string? name = null;
            string? usfmPath = null;
            string? vplPath = null;
            string? reportPath = null;
            var expectedBookCount = 66;
            var maxDifferenceSamples = 20;
            var requireStrongAttributes = false;

            for (var index = 0; index < args.Count; index++)
            {
                var option = args[index];
                switch (option)
                {
                    case "--require-strong":
                        requireStrongAttributes = true;
                        break;
                    case "--name":
                        name = ReadValue(args, ref index, option);
                        break;
                    case "--usfm":
                        usfmPath = ReadValue(args, ref index, option);
                        break;
                    case "--vpl":
                        vplPath = ReadValue(args, ref index, option);
                        break;
                    case "--report":
                        reportPath = ReadValue(args, ref index, option);
                        break;
                    case "--expected-books":
                        expectedBookCount = ReadPositiveInteger(args, ref index, option);
                        break;
                    case "--max-samples":
                        maxDifferenceSamples = ReadPositiveInteger(args, ref index, option);
                        break;
                    default:
                        throw new BibleCorpusException($"Unknown option: {option}");
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BibleCorpusException("Missing required option --name.");
            }

            if (string.IsNullOrWhiteSpace(usfmPath))
            {
                throw new BibleCorpusException("Missing required option --usfm.");
            }

            if (string.IsNullOrWhiteSpace(vplPath))
            {
                throw new BibleCorpusException("Missing required option --vpl.");
            }

            return new BenchmarkOptions(
                name,
                usfmPath,
                vplPath,
                expectedBookCount,
                requireStrongAttributes,
                maxDifferenceSamples,
                reportPath);
        }

        private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
        {
            index++;
            if (index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new BibleCorpusException($"Missing value for {option}.");
            }

            return args[index];
        }

        private static int ReadPositiveInteger(IReadOnlyList<string> args, ref int index, string option)
        {
            var value = ReadValue(args, ref index, option);
            if (!int.TryParse(value, out var parsed) || parsed < 1)
            {
                throw new BibleCorpusException($"{option} must be a positive integer.");
            }

            return parsed;
        }
    }
}
