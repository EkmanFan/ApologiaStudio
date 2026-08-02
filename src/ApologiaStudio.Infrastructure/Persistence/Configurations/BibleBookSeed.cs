using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal static class BibleBookSeed
{
    public static IReadOnlyList<BibleBookEntity> All { get; } =
    [
        Book("GEN", "Gen", 1), Book("EXO", "Exod", 2), Book("LEV", "Lev", 3),
        Book("NUM", "Num", 4), Book("DEU", "Deut", 5), Book("JOS", "Josh", 6),
        Book("JDG", "Judg", 7), Book("RUT", "Ruth", 8), Book("1SA", "1Sam", 9),
        Book("2SA", "2Sam", 10), Book("1KI", "1Kgs", 11), Book("2KI", "2Kgs", 12),
        Book("1CH", "1Chr", 13), Book("2CH", "2Chr", 14), Book("EZR", "Ezra", 15),
        Book("NEH", "Neh", 16), Book("EST", "Esth", 17), Book("JOB", "Job", 18),
        Book("PSA", "Ps", 19), Book("PRO", "Prov", 20), Book("ECC", "Eccl", 21),
        Book("SNG", "Song", 22), Book("ISA", "Isa", 23), Book("JER", "Jer", 24),
        Book("LAM", "Lam", 25), Book("EZK", "Ezek", 26), Book("DAN", "Dan", 27),
        Book("HOS", "Hos", 28), Book("JOL", "Joel", 29), Book("AMO", "Amos", 30),
        Book("OBA", "Obad", 31), Book("JON", "Jonah", 32), Book("MIC", "Mic", 33),
        Book("NAM", "Nah", 34), Book("HAB", "Hab", 35), Book("ZEP", "Zeph", 36),
        Book("HAG", "Hag", 37), Book("ZEC", "Zech", 38), Book("MAL", "Mal", 39),
        Book("MAT", "Matt", 40), Book("MRK", "Mark", 41), Book("LUK", "Luke", 42),
        Book("JHN", "John", 43), Book("ACT", "Acts", 44), Book("ROM", "Rom", 45),
        Book("1CO", "1Cor", 46), Book("2CO", "2Cor", 47), Book("GAL", "Gal", 48),
        Book("EPH", "Eph", 49), Book("PHP", "Phil", 50), Book("COL", "Col", 51),
        Book("1TH", "1Thess", 52), Book("2TH", "2Thess", 53), Book("1TI", "1Tim", 54),
        Book("2TI", "2Tim", 55), Book("TIT", "Titus", 56), Book("PHM", "Phlm", 57),
        Book("HEB", "Heb", 58), Book("JAS", "Jas", 59), Book("1PE", "1Pet", 60),
        Book("2PE", "2Pet", 61), Book("1JN", "1John", 62), Book("2JN", "2John", 63),
        Book("3JN", "3John", 64), Book("JUD", "Jude", 65), Book("REV", "Rev", 66)
    ];

    private static BibleBookEntity Book(string usfmCode, string osisCode, int canonicalOrder) =>
        new()
        {
            UsfmCode = new UsfmBookCode(usfmCode),
            OsisCode = osisCode,
            CanonicalOrder = canonicalOrder,
            CanonCode = "protestant-66"
        };
}
