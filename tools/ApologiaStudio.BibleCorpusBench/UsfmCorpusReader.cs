using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;

namespace ApologiaStudio.BibleCorpusBench;

public sealed class UsfmCorpusReader
{
    public CorpusReadResult Read(
        string path,
        IEnumerable<UsfmBookCode>? excludedBookCodes = null)
    {
        try
        {
            var parsed = new SilMachineUsfmCorpusReader()
                .ReadAsync(
                    new BibleCorpusReadRequest(path, excludedBookCodes),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var verses = parsed.Verses.ToDictionary(
                verse => new VerseKey(
                    verse.Reference.BookCode.Value,
                    verse.Reference.ChapterNumber,
                    verse.Reference.VerseLabel),
                verse => new BibleVerse(
                    new VerseKey(
                        verse.Reference.BookCode.Value,
                        verse.Reference.ChapterNumber,
                        verse.Reference.VerseLabel),
                    verse.Text,
                    verse.SourceRelativePath,
                    verse.SourceLine,
                    verse.WordAnnotations
                        .Select(annotation => new ParsedWordAnnotation(
                            annotation.Marker,
                            annotation.Name,
                            annotation.Value,
                            annotation.CharacterOffset,
                            annotation.CharacterLength))
                        .ToArray(),
                    verse.SupplementalTexts
                        .Select(supplemental => new ParsedSupplementalText(
                            supplemental.Marker,
                            supplemental.Text,
                            GetComparisonOffset(supplemental, verse.Text.Length),
                            supplemental.Placement != BibleSupplementalTextPlacement.Before))
                        .ToArray()));
            var strongAttributeCount = parsed.Verses
                .SelectMany(verse => verse.WordAnnotations)
                .Count(annotation => string.Equals(
                    annotation.Name,
                    "strong",
                    StringComparison.OrdinalIgnoreCase));

            return new CorpusReadResult(
                verses,
                parsed.SourceFileCount,
                parsed.Books.Count,
                strongAttributeCount);
        }
        catch (BibleCorpusReadException exception)
        {
            throw new BibleCorpusException(exception.Message, exception);
        }
    }

    private static int GetComparisonOffset(
        ParsedBibleSupplementalText supplemental,
        int verseTextLength) => supplemental.Placement switch
        {
            BibleSupplementalTextPlacement.Before => 0,
            BibleSupplementalTextPlacement.Within => supplemental.CharacterOffset!.Value,
            BibleSupplementalTextPlacement.After => verseTextLength,
            _ => throw new BibleCorpusException(
                $"Unsupported supplemental placement {supplemental.Placement}.")
        };
}
