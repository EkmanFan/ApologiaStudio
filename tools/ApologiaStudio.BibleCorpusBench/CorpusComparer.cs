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
                GetComparisonText(usfm.Verses[key]),
                GetComparisonText(vpl.Verses[key]),
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
                new ReferenceDifference(
                    key.ToString(),
                    GetComparisonText(usfm.Verses[key]),
                    GetComparisonText(vpl.Verses[key]))))
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

    private static string GetComparisonText(BibleVerse verse)
    {
        if (verse.SupplementalTexts.Count == 0)
        {
            return verse.Text;
        }

        var result = new System.Text.StringBuilder();
        var textOffset = 0;
        foreach (var supplemental in verse.SupplementalTexts
                     .Where(item => ShouldFlattenForVplComparison(item, verse.Text.Length))
                     .Select((value, index) => (value, index))
                     .OrderBy(item => item.value.CharacterOffset)
                     .ThenBy(item => item.index)
                     .Select(item => item.value))
        {
            if (supplemental.CharacterOffset < textOffset
                || supplemental.CharacterOffset > verse.Text.Length)
            {
                throw new BibleCorpusException(
                    $"Invalid supplemental text offset {supplemental.CharacterOffset} for {verse.Key}.");
            }

            result.Append(verse.Text, textOffset, supplemental.CharacterOffset - textOffset);
            result.Append(' ');
            result.Append(supplemental.Text);
            result.Append(' ');
            textOffset = supplemental.CharacterOffset;
        }

        result.Append(verse.Text, textOffset, verse.Text.Length - textOffset);
        return TextNormalizer.Normalize(result.ToString());
    }

    private static bool ShouldFlattenForVplComparison(
        ParsedSupplementalText supplemental,
        int verseTextLength)
    {
        // eBible VPL includes inline speaker labels but omits standalone speaker
        // headings placed between verses. Descriptive titles are always flattened
        // at their structural position.
        return !string.Equals(supplemental.Marker, "sp", StringComparison.Ordinal)
            || (supplemental.OccurredWithinVerse
                && supplemental.CharacterOffset < verseTextLength);
    }
}
