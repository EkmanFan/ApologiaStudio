using ApologiaStudio.Application.BibleCorpora.Queries;

namespace ApologiaStudio.UnitTests.Application.BibleCorpora;

public sealed class BibleCorpusQueryModelTests
{
    [Fact]
    public void Chapter_DefensivelyCopiesVersesAndAnnotations()
    {
        var annotations = new List<BibleWordAnnotation>
        {
            new(1, "w", "strong", "G3056", 0, 5)
        };

        var verses = new List<BibleVerseText>
        {
            new("JHN", 1, "1", 1, "Au commencement", annotations)
        };

        var chapter = new BibleChapter(
            new BibleEditionSummary(
                "lsg1910",
                "Louis Segond 1910",
                "fr",
                "protestant-66"),
            new BibleBookSummary(
                "JHN",
                "John",
                43,
                "Jean",
                "Jn",
                21),
            1,
            verses);

        annotations.Clear();
        verses.Clear();

        var verse = Assert.Single(chapter.Verses);
        Assert.Single(verse.WordAnnotations);
    }
}
