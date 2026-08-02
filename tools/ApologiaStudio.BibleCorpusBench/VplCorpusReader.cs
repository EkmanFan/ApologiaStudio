using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ApologiaStudio.BibleCorpusBench;

public sealed partial class VplCorpusReader
{
    private static readonly IReadOnlySet<string> Extensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt", ".vpl" };

    public CorpusReadResult Read(string path)
    {
        var files = CorpusFileDiscovery.Find(path, Extensions, "VPL");
        var verses = new Dictionary<VerseKey, BibleVerse>();

        foreach (var file in files)
        {
            ReadFile(file, verses);
        }

        var bookCount = verses.Keys.Select(key => key.BookCode).Distinct(StringComparer.Ordinal).Count();
        return new CorpusReadResult(verses, files.Count, bookCount, 0);
    }

    private static void ReadFile(string file, IDictionary<VerseKey, BibleVerse> verses)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(file, new UTF8Encoding(false, true));
        }
        catch (Exception exception)
        {
            throw new BibleCorpusException($"Unable to read VPL file {file}: {exception.Message}", exception);
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd().TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var match = VplLineRegex().Match(line);
            if (!match.Success)
            {
                throw new BibleCorpusException(
                    $"{file}:{index + 1}: Invalid VPL line. Expected 'BOOK chapter:verse [text]'.");
            }

            if (!int.TryParse(
                    match.Groups["chapter"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var chapter))
            {
                throw new BibleCorpusException($"{file}:{index + 1}: Invalid VPL chapter number.");
            }

            var key = new VerseKey(
                NormalizeBookCode(match.Groups["book"].Value),
                chapter,
                match.Groups["verse"].Value);
            var verse = new BibleVerse(
                key,
                TextNormalizer.Normalize(match.Groups["text"].Value),
                file,
                index + 1,
                Array.Empty<ParsedWordAnnotation>(),
                Array.Empty<ParsedSupplementalText>());

            if (verses.TryGetValue(key, out var existing))
            {
                throw new BibleCorpusException(
                    $"Duplicate VPL reference {key} at {existing.Source}:{existing.SourceLine} "
                    + $"and {file}:{index + 1}.");
            }

            verses.Add(key, verse);
        }
    }

    private static string NormalizeBookCode(string bookCode)
    {
        // eBible's BibleWorks VPL exports use a few legacy abbreviations that
        // differ from the canonical USFM book identifiers emitted by the USFM
        // parser. Normalize only those known aliases; VerseKey handles casing.
        return bookCode.ToUpperInvariant() switch
        {
            "1JO" => "1JN",
            "2JO" => "2JN",
            "3JO" => "3JN",
            "EZE" => "EZK",
            "JAM" => "JAS",
            "JOE" => "JOL",
            "JOH" => "JHN",
            "MAR" => "MRK",
            "NAH" => "NAM",
            "PHI" => "PHP",
            "SOL" => "SNG",
            _ => bookCode
        };
    }

    [GeneratedRegex(
        "^(?<book>[1-3]?[A-Za-z]{2,3})\\s+(?<chapter>[0-9]+):(?<verse>[0-9]+[A-Za-z]?(?:[-,][0-9]+[A-Za-z]?)*)(?:\\s+(?<text>.*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex VplLineRegex();
}
