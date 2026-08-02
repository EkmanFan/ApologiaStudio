namespace ApologiaStudio.BibleCorpusBench;

public sealed class CorpusComparer
{
    public CorpusValidationReport Compare(
        string corpusName,
        CorpusReadResult usfm,
        CorpusReadResult vpl,
        int expectedBookCount,
        bool requireStrongAttributes,
        int maxDifferenceSamples)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusName);
        ArgumentNullException.ThrowIfNull(usfm);
        ArgumentNullException.ThrowIfNull(vpl);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedBookCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDifferenceSamples, 1);

        var missingFromUsfm = vpl.Verses.Keys
            .Except(usfm.Verses.Keys)
            .OrderBy(key => key.BookCode, StringComparer.Ordinal)
            .ThenBy(key => key.Chapter)
            .ThenBy(key => key.Verse, StringComparer.Ordinal)
            .ToArray();
        var unexpectedInUsfm = usfm.Verses.Keys
            .Except(vpl.Verses.Keys)
            .OrderBy(key => key.BookCode, StringComparer.Ordinal)
            .ThenBy(key => key.Chapter)
            .ThenBy(key => key.Verse, StringComparer.Ordinal)
            .ToArray();
        var textMismatches = usfm.Verses.Keys
            .Intersect(vpl.Verses.Keys)
            .Where(key => !string.Equals(
                usfm.Verses[key].Text,
                vpl.Verses[key].Text,
                StringComparison.Ordinal))
            .OrderBy(key => key.BookCode, StringComparer.Ordinal)
            .ThenBy(key => key.Chapter)
            .ThenBy(key => key.Verse, StringComparer.Ordinal)
            .ToArray();

        var differences = missingFromUsfm
            .Select(key => new ReferenceDifference(key.ToString(), null, vpl.Verses[key].Text))
            .Concat(unexpectedInUsfm.Select(key =>
                new ReferenceDifference(key.ToString(), usfm.Verses[key].Text, null)))
            .Concat(textMismatches.Select(key =>
                new ReferenceDifference(key.ToString(), usfm.Verses[key].Text, vpl.Verses[key].Text)))
            .Take(maxDifferenceSamples)
            .ToArray();

        var isMatch = usfm.BookCount == expectedBookCount
            && vpl.BookCount == expectedBookCount
            && (!requireStrongAttributes || usfm.StrongAttributeCount > 0)
            && missingFromUsfm.Length == 0
            && unexpectedInUsfm.Length == 0
            && textMismatches.Length == 0;

        return new CorpusValidationReport(
            corpusName,
            DateTimeOffset.UtcNow,
            expectedBookCount,
            requireStrongAttributes,
            usfm.FileCount,
            usfm.BookCount,
            usfm.Verses.Count,
            vpl.FileCount,
            vpl.BookCount,
            vpl.Verses.Count,
            usfm.StrongAttributeCount,
            missingFromUsfm.Length,
            unexpectedInUsfm.Length,
            textMismatches.Length,
            differences,
            isMatch);
    }
}
