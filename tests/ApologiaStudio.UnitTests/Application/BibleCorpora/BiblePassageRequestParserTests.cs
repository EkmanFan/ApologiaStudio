using ApologiaStudio.Application.BibleCorpora.Queries;

namespace ApologiaStudio.UnitTests.Application.BibleCorpora;

public sealed class BiblePassageRequestParserTests
{
    private readonly BiblePassageRequestParser _parser = new();

    [Theory]
    [InlineData("Jean 3:16", null, "JHN", 3, "16", null)]
    [InlineData("Peux-tu citer 1 Corinthiens 13:4 ?", null, "1CO", 13, "4", null)]
    [InlineData("Peux-tu citer 1 Corinthiens 13 ?", null, "1CO", 13, null, null)]
    [InlineData("Please read John 3:16.", null, "JHN", 3, "16", null)]
    [InlineData("Please read 1 Corinthians 13.", null, "1CO", 13, null, null)]
    [InlineData("Daniel 3:16 (WEB)", "web-classic", "DAN", 3, "16", null)]
    [InlineData("Jean 3:16 en anglais", "web-classic", "JHN", 3, "16", null)]
    [InlineData("John 3:16 in French", "lsg1910", "JHN", 3, "16", null)]
    [InlineData("JHN 3:16", null, "JHN", 3, "16", null)]
    [InlineData("Jean 3:16-18", null, "JHN", 3, "16", "18")]
    public void TryParse_ShouldRecognizeSupportedReference(
        string input,
        string? expectedEdition,
        string expectedBook,
        int expectedChapter,
        string? expectedVerse,
        string? expectedEndVerse)
    {
        var wasParsed = _parser.TryParse(
            input,
            out var request);

        Assert.True(wasParsed);
        Assert.Equal(
            expectedEdition,
            request.RequestedEditionCode?.Value);
        Assert.Equal(expectedBook, request.BookCode.Value);
        Assert.Equal(expectedChapter, request.ChapterNumber);
        Assert.Equal(expectedVerse, request.VerseLabel);
        Assert.Equal(expectedEndVerse, request.EndVerseLabel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Jean chapitre trois verset seize")]
    [InlineData("Rendez-vous à 3:16")]
    [InlineData("Jean 0:16")]
    [InlineData("1 Corinthien 13")]
    [InlineData("Jena 3:16")]
    public void TryParse_ShouldRejectUnsupportedInput(
        string input)
    {
        Assert.False(
            _parser.TryParse(
                input,
                out _));
    }

    [Theory]
    [InlineData("1 Corinthien 13", null)]
    [InlineData("1 Corinthien 13 in English", "web-classic")]
    [InlineData("Jena 3:16 en français", "lsg1910")]
    public void GetExplicitlyRequestedEdition_ShouldNotDependOnBookParsing(
        string input,
        string? expectedEdition)
    {
        var edition = BiblePassageRequestParser
            .GetExplicitlyRequestedEdition(input);

        Assert.Equal(expectedEdition, edition?.Value);
    }

    [Theory]
    [InlineData("1 Corinthien 13 in English", "1 corinthien 13")]
    [InlineData("Jean 3:16 en français", "jean 3:16")]
    [InlineData("John 3:16 WEB Classic", "john 3:16")]
    public void RemoveExplicitEditionRequest_ShouldKeepReferenceOnly(
        string input,
        string expected)
    {
        Assert.Equal(
            expected,
            BiblePassageRequestParser
                .RemoveExplicitEditionRequest(input));
    }

    [Theory]
    [InlineData("Compare Jean 3:16 et Romains 5:8")]
    public void ContainsReferenceCandidate_ShouldIdentifyUnsupportedPassage(
        string input)
    {
        Assert.False(
            _parser.TryParse(
                input,
                out _));

        Assert.True(
            _parser.ContainsReferenceCandidate(
                input));
    }

    [Theory]
    [InlineData("Jean 3:16", true)]
    [InlineData("Peux-tu citer Jean 3:16 ?", true)]
    [InlineData("Please read John 3:16.", true)]
    [InlineData("1 Corinthien 13", true)]
    [InlineData("1 Corinthien 13 en français", true)]
    [InlineData("1 Corinthien 13 in English", true)]
    [InlineData("John 3:16 in French", true)]
    [InlineData("Jena 3:16", true)]
    [InlineData("Explique-moi Jean 3:16.", false)]
    [InlineData("Compare Jean 3:16 et Romains 5:8.", false)]
    public void IsPassageLookupRequest_ShouldSeparateLookupFromExegesis(
        string input,
        bool expected)
    {
        Assert.Equal(
            expected,
            _parser.IsPassageLookupRequest(input));
    }
}
