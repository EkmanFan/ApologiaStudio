using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.UnitTests.Application.BibleCorpora;

public sealed class BibleCorpusIngestionContractTests
{
    [Fact]
    public void ReadRequest_copies_and_deduplicates_excluded_book_codes()
    {
        var excluded = new[]
        {
            new UsfmBookCode("FRT"),
            new UsfmBookCode("FRT"),
            new UsfmBookCode("GLO")
        };

        var request = new BibleCorpusReadRequest(" /corpora/web ", excluded);

        Assert.Equal("/corpora/web", request.SourceDirectory);
        Assert.Equal(2, request.ExcludedBookCodes.Count);
    }

    [Fact]
    public void ReadResult_defensively_copies_the_reader_output()
    {
        var books = new List<ParsedBibleBook>
        {
            new(new UsfmBookCode("GEN"), 1, "Genesis", "Gen", "01-GEN.usfm")
        };
        var verses = new List<ParsedBibleVerse>
        {
            CreateVerse()
        };

        var result = new BibleCorpusReadResult(1, books, verses);
        books.Clear();
        verses.Clear();

        Assert.Single(result.Books);
        Assert.Single(result.Verses);
    }

    [Fact]
    public void ParsedVerse_retains_complete_annotation_spans()
    {
        var annotation = new ParsedBibleWordAnnotation(
            1,
            "w",
            "strong",
            "H7225",
            0,
            12);

        var verse = CreateVerse([annotation]);

        var actual = Assert.Single(verse.WordAnnotations);
        Assert.Equal(0, actual.CharacterOffset);
        Assert.Equal(12, actual.CharacterLength);
    }

    [Fact]
    public void Supplemental_text_within_a_verse_requires_an_offset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParsedBibleSupplementalText(
                1,
                "sp",
                "The woman",
                BibleSupplementalTextPlacement.Within,
                null));
    }

    [Fact]
    public void Supplemental_text_outside_a_verse_rejects_an_offset()
    {
        Assert.Throws<ArgumentException>(() =>
            new ParsedBibleSupplementalText(
                1,
                "d",
                "A Psalm of David",
                BibleSupplementalTextPlacement.Before,
                0));
    }

    private static ParsedBibleVerse CreateVerse(
        IEnumerable<ParsedBibleWordAnnotation>? annotations = null)
    {
        return new ParsedBibleVerse(
            new BibleReference(new UsfmBookCode("GEN"), 1, "1"),
            1,
            "In the beginning",
            "01-GEN.usfm",
            12,
            annotations);
    }
}
