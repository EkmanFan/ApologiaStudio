using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Queries;

public sealed record BiblePassageRequest(
    BibleEditionCode? RequestedEditionCode,
    UsfmBookCode BookCode,
    int ChapterNumber,
    string? VerseLabel,
    string? EndVerseLabel = null);

public sealed class BiblePassageRequestParser
{
    private const string FrenchEditionCode = "lsg1910";
    private const string EnglishEditionCode = "web-classic";

    private static readonly string[] CanonicalBookCodes =
    [
        "GEN", "EXO", "LEV", "NUM", "DEU", "JOS", "JDG", "RUT",
        "1SA", "2SA", "1KI", "2KI", "1CH", "2CH", "EZR", "NEH",
        "EST", "JOB", "PSA", "PRO", "ECC", "SNG", "ISA", "JER",
        "LAM", "EZK", "DAN", "HOS", "JOL", "AMO", "OBA", "JON",
        "MIC", "NAM", "HAB", "ZEP", "HAG", "ZEC", "MAL", "MAT",
        "MRK", "LUK", "JHN", "ACT", "ROM", "1CO", "2CO", "GAL",
        "EPH", "PHP", "COL", "1TH", "2TH", "1TI", "2TI", "TIT",
        "PHM", "HEB", "JAS", "1PE", "2PE", "1JN", "2JN", "3JN",
        "JUD", "REV"
    ];

    public static IReadOnlyList<string> SupportedBookCodes { get; } =
        Array.AsReadOnly(CanonicalBookCodes);

    private static readonly Regex FrenchEditionRegex = new(
        @"(?<![\p{L}\p{N}])(?:lsg(?:\s*1910)?|louis\s+segond(?:\s*1910)?|(?:en|in)\s+(?:francais|french)|francais|french)(?![\p{L}\p{N}])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EnglishEditionRegex = new(
        @"(?<![\p{L}\p{N}])(?:web(?:[\s-]+classic)?|world\s+english\s+bible(?:[\s-]+classic)?|(?:en|in)\s+(?:anglais|english)|anglais|english)(?![\p{L}\p{N}])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LookupVerbRegex = new(
        @"(?<![\p{L}\p{N}])(?:affiche|cite|citer|donne|donner|lis|lire|quote|read|show)(?![\p{L}\p{N}])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InterpretationIntentRegex = new(
        @"(?<![\p{L}\p{N}])(?:analyse|analyser|compare|comparer|explique|expliquer|interpretation|interprete|interpreter|pourquoi|signification|signifie|analyze|compare|explain|interpret|mean|means|why)(?![\p{L}\p{N}])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NonSemanticRemainderRegex = new(
        @"[\s\p{P}\p{S}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareReferenceShapeRegex = new(
        @"^\s*(?:(?:[1-3]\s+)[\p{L}'’-]{2,}(?:\s+[\p{L}'’-]{2,}){0,3}\s+[0-9]{1,3}(?:\s*:\s*[0-9]{1,3}(?:\s*-\s*[0-9]{1,3})?)?|[\p{L}'’-]{2,}(?:\s+[\p{L}'’-]{2,}){0,3}\s+[0-9]{1,3}\s*:\s*[0-9]{1,3}(?:\s*-\s*[0-9]{1,3})?)\s*[.!?]?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, BookAlias>
        BookAliases = CreateBookAliases();

    private static readonly Regex ReferenceRegex =
        CreateReferenceRegex();

    private static readonly Regex ReferenceCandidateRegex =
        CreateReferenceCandidateRegex();

    public bool TryParse(
        string input,
        out BiblePassageRequest request)
    {
        request = null!;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalizedInput = Normalize(input);
        var explicitlyRequestedEdition =
            FindExplicitEdition(normalizedInput);

        var matches = ReferenceRegex.Matches(
            normalizedInput);

        if (matches.Count != 1)
        {
            return false;
        }

        var match = matches[0];

        if (!int.TryParse(
                match.Groups["chapter"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var chapterNumber) ||
            chapterNumber < 1)
        {
            return false;
        }

        var bookAlias = BookAliases[
            match.Groups["book"].Value];

        request = new BiblePassageRequest(
            explicitlyRequestedEdition,
            new UsfmBookCode(bookAlias.BookCode),
            chapterNumber,
            match.Groups["verse"].Success
                ? match.Groups["verse"].Value
                : null,
            match.Groups["verseEnd"].Success
                ? match.Groups["verseEnd"].Value
                : null);

        return true;
    }

    public static BibleEditionCode? GetExplicitlyRequestedEdition(
        string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return FindExplicitEdition(Normalize(input));
    }

    public static string RemoveExplicitEditionRequest(
        string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var normalizedInput = Normalize(input);

        return EnglishEditionRegex.Replace(
                FrenchEditionRegex.Replace(
                    normalizedInput,
                    string.Empty),
                string.Empty)
            .Trim();
    }

    public bool ContainsReferenceCandidate(string input)
    {
        return !string.IsNullOrWhiteSpace(input) &&
               ReferenceCandidateRegex.IsMatch(
                   Normalize(input));
    }

    public bool IsPassageLookupRequest(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalizedInput = Normalize(input);

        if (InterpretationIntentRegex.IsMatch(normalizedInput))
        {
            return false;
        }

        var inputWithoutExplicitEdition =
            EnglishEditionRegex.Replace(
                FrenchEditionRegex.Replace(
                    normalizedInput,
                    string.Empty),
                string.Empty);

        if (BareReferenceShapeRegex.IsMatch(
                inputWithoutExplicitEdition))
        {
            return true;
        }

        if (LookupVerbRegex.IsMatch(normalizedInput))
        {
            return true;
        }

        var matches = ReferenceRegex.Matches(normalizedInput);

        if (matches.Count != 1)
        {
            return false;
        }

        var remainder = ReferenceRegex.Replace(
            normalizedInput,
            string.Empty);

        remainder = FrenchEditionRegex.Replace(
            remainder,
            string.Empty);

        remainder = EnglishEditionRegex.Replace(
            remainder,
            string.Empty);

        return NonSemanticRemainderRegex.Replace(
                remainder,
                string.Empty)
            .Length == 0;
    }

    private static BibleEditionCode? FindExplicitEdition(
        string normalizedInput)
    {
        var hasFrenchEdition =
            FrenchEditionRegex.IsMatch(normalizedInput);

        var hasEnglishEdition =
            EnglishEditionRegex.IsMatch(normalizedInput);

        if (hasFrenchEdition == hasEnglishEdition)
        {
            return null;
        }

        return hasFrenchEdition
            ? new BibleEditionCode(FrenchEditionCode)
            : new BibleEditionCode(EnglishEditionCode);
    }

    private static IReadOnlyDictionary<string, BookAlias>
        CreateBookAliases()
    {
        var aliases = new List<BookAlias>();

        AddFrenchAliases(aliases);
        AddEnglishAliases(aliases);
        AddUsfmAliases(aliases);

        return aliases
            .GroupBy(alias => alias.Alias)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
    }

    private static Regex CreateReferenceRegex()
    {
        var bookAlternation = CreateBookAlternation();

        return new Regex(
            $@"(?<![\p{{L}}\p{{N}}])(?<book>{bookAlternation})\s+(?<chapter>[0-9]{{1,3}})(?:\s*:\s*(?<verse>[0-9]{{1,3}}[a-z]?)(?:\s*-\s*(?<verseEnd>[0-9]{{1,3}}[a-z]?))?(?![\p{{L}}\p{{N}},-])|(?!\s*:)(?![\p{{L}}\p{{N}}]))",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);
    }

    private static Regex CreateReferenceCandidateRegex()
    {
        var bookAlternation = CreateBookAlternation();

        return new Regex(
            $@"(?<![\p{{L}}\p{{N}}])(?:{bookAlternation})\s+[0-9]{{1,3}}(?![0-9])",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);
    }

    private static string CreateBookAlternation()
    {
        return string.Join(
            "|",
            BookAliases.Keys
                .OrderByDescending(alias => alias.Length)
                .Select(Regex.Escape));
    }

    private static void AddFrenchAliases(
        ICollection<BookAlias> aliases)
    {
        Add(aliases, "GEN", "genese");
        Add(aliases, "EXO", "exode");
        Add(aliases, "LEV", "levitique");
        Add(aliases, "NUM", "nombres");
        Add(aliases, "DEU", "deuteronome");
        Add(aliases, "JOS", "josue");
        Add(aliases, "JDG", "juges");
        Add(aliases, "RUT", "ruth");
        Add(aliases, "1SA", "1 samuel");
        Add(aliases, "2SA", "2 samuel");
        Add(aliases, "1KI", "1 rois");
        Add(aliases, "2KI", "2 rois");
        Add(aliases, "1CH", "1 chroniques");
        Add(aliases, "2CH", "2 chroniques");
        Add(aliases, "EZR", "esdras");
        Add(aliases, "NEH", "nehemie");
        Add(aliases, "EST", "esther");
        Add(aliases, "JOB", "job");
        Add(aliases, "PSA", "psaume", "psaumes");
        Add(aliases, "PRO", "proverbes");
        Add(aliases, "ECC", "ecclesiaste");
        Add(aliases, "SNG", "cantique des cantiques");
        Add(aliases, "ISA", "esaie", "isaie");
        Add(aliases, "JER", "jeremie");
        Add(aliases, "LAM", "lamentations");
        Add(aliases, "EZK", "ezechiel");
        Add(aliases, "DAN", "daniel");
        Add(aliases, "HOS", "osee");
        Add(aliases, "JOL", "joel");
        Add(aliases, "AMO", "amos");
        Add(aliases, "OBA", "abdias");
        Add(aliases, "JON", "jonas");
        Add(aliases, "MIC", "michee");
        Add(aliases, "NAM", "nahum");
        Add(aliases, "HAB", "habacuc");
        Add(aliases, "ZEP", "sophonie");
        Add(aliases, "HAG", "aggee");
        Add(aliases, "ZEC", "zacharie");
        Add(aliases, "MAL", "malachie");
        Add(aliases, "MAT", "matthieu");
        Add(aliases, "MRK", "marc");
        Add(aliases, "LUK", "luc");
        Add(aliases, "JHN", "jean");
        Add(aliases, "ACT", "actes", "actes des apotres");
        Add(aliases, "ROM", "romains");
        Add(aliases, "1CO", "1 corinthiens");
        Add(aliases, "2CO", "2 corinthiens");
        Add(aliases, "GAL", "galates");
        Add(aliases, "EPH", "ephesiens");
        Add(aliases, "PHP", "philippiens");
        Add(aliases, "COL", "colossiens");
        Add(aliases, "1TH", "1 thessaloniciens");
        Add(aliases, "2TH", "2 thessaloniciens");
        Add(aliases, "1TI", "1 timothee");
        Add(aliases, "2TI", "2 timothee");
        Add(aliases, "TIT", "tite");
        Add(aliases, "PHM", "philemon");
        Add(aliases, "HEB", "hebreux");
        Add(aliases, "JAS", "jacques");
        Add(aliases, "1PE", "1 pierre");
        Add(aliases, "2PE", "2 pierre");
        Add(aliases, "1JN", "1 jean");
        Add(aliases, "2JN", "2 jean");
        Add(aliases, "3JN", "3 jean");
        Add(aliases, "JUD", "jude");
        Add(aliases, "REV", "apocalypse");
    }

    private static void AddEnglishAliases(
        ICollection<BookAlias> aliases)
    {
        Add(aliases, "GEN", "genesis");
        Add(aliases, "EXO", "exodus");
        Add(aliases, "LEV", "leviticus");
        Add(aliases, "NUM", "numbers");
        Add(aliases, "DEU", "deuteronomy");
        Add(aliases, "JOS", "joshua");
        Add(aliases, "JDG", "judges");
        Add(aliases, "RUT", "ruth");
        Add(aliases, "1SA", "1 samuel");
        Add(aliases, "2SA", "2 samuel");
        Add(aliases, "1KI", "1 kings");
        Add(aliases, "2KI", "2 kings");
        Add(aliases, "1CH", "1 chronicles");
        Add(aliases, "2CH", "2 chronicles");
        Add(aliases, "EZR", "ezra");
        Add(aliases, "NEH", "nehemiah");
        Add(aliases, "EST", "esther");
        Add(aliases, "JOB", "job");
        Add(aliases, "PSA", "psalm", "psalms");
        Add(aliases, "PRO", "proverbs");
        Add(aliases, "ECC", "ecclesiastes");
        Add(aliases, "SNG", "song of songs", "song of solomon");
        Add(aliases, "ISA", "isaiah");
        Add(aliases, "JER", "jeremiah");
        Add(aliases, "LAM", "lamentations");
        Add(aliases, "EZK", "ezekiel");
        Add(aliases, "DAN", "daniel");
        Add(aliases, "HOS", "hosea");
        Add(aliases, "JOL", "joel");
        Add(aliases, "AMO", "amos");
        Add(aliases, "OBA", "obadiah");
        Add(aliases, "JON", "jonah");
        Add(aliases, "MIC", "micah");
        Add(aliases, "NAM", "nahum");
        Add(aliases, "HAB", "habakkuk");
        Add(aliases, "ZEP", "zephaniah");
        Add(aliases, "HAG", "haggai");
        Add(aliases, "ZEC", "zechariah");
        Add(aliases, "MAL", "malachi");
        Add(aliases, "MAT", "matthew");
        Add(aliases, "MRK", "mark");
        Add(aliases, "LUK", "luke");
        Add(aliases, "JHN", "john");
        Add(aliases, "ACT", "acts", "acts of the apostles");
        Add(aliases, "ROM", "romans");
        Add(aliases, "1CO", "1 corinthians");
        Add(aliases, "2CO", "2 corinthians");
        Add(aliases, "GAL", "galatians");
        Add(aliases, "EPH", "ephesians");
        Add(aliases, "PHP", "philippians");
        Add(aliases, "COL", "colossians");
        Add(aliases, "1TH", "1 thessalonians");
        Add(aliases, "2TH", "2 thessalonians");
        Add(aliases, "1TI", "1 timothy");
        Add(aliases, "2TI", "2 timothy");
        Add(aliases, "TIT", "titus");
        Add(aliases, "PHM", "philemon");
        Add(aliases, "HEB", "hebrews");
        Add(aliases, "JAS", "james");
        Add(aliases, "1PE", "1 peter");
        Add(aliases, "2PE", "2 peter");
        Add(aliases, "1JN", "1 john");
        Add(aliases, "2JN", "2 john");
        Add(aliases, "3JN", "3 john");
        Add(aliases, "JUD", "jude");
        Add(aliases, "REV", "revelation");
    }

    private static void AddUsfmAliases(
        ICollection<BookAlias> aliases)
    {
        foreach (var code in CanonicalBookCodes)
        {
            Add(
                aliases,
                code,
                code.ToLowerInvariant());
        }
    }

    private static void Add(
        ICollection<BookAlias> aliases,
        string bookCode,
        params string[] bookAliases)
    {
        foreach (var bookAlias in bookAliases)
        {
            aliases.Add(
                new BookAlias(
                    Normalize(bookAlias),
                    bookCode));
        }
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(
            NormalizationForm.FormD);

        var builder = new StringBuilder(
            decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(
                char.ToLowerInvariant(character));
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private sealed record BookAlias(
        string Alias,
        string BookCode);

}
