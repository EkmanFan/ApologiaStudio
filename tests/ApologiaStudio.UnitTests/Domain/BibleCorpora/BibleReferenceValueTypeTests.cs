using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.UnitTests.Domain.BibleCorpora;

public sealed class BibleReferenceValueTypeTests
{
    [Fact]
    public void BibleEditionCode_normalizes_a_valid_code()
    {
        var code = new BibleEditionCode(" WEB-Classic ");

        Assert.Equal("web-classic", code.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-web")]
    [InlineData("web--classic")]
    [InlineData("web_classic")]
    public void BibleEditionCode_rejects_an_invalid_code(string value)
    {
        Assert.Throws<ArgumentException>(() => new BibleEditionCode(value));
    }

    [Theory]
    [InlineData("gen", "GEN")]
    [InlineData(" 1co ", "1CO")]
    public void UsfmBookCode_normalizes_a_valid_code(string value, string expected)
    {
        var code = new UsfmBookCode(value);

        Assert.Equal(expected, code.Value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("4MA")]
    [InlineData("GENESIS")]
    [InlineData("G-N")]
    public void UsfmBookCode_rejects_an_invalid_code(string value)
    {
        Assert.Throws<ArgumentException>(() => new UsfmBookCode(value));
    }

    [Theory]
    [InlineData("3a")]
    [InlineData("4-5")]
    [InlineData("7,8")]
    public void BibleReference_preserves_supported_USFM_verse_labels(string verseLabel)
    {
        var reference = new BibleReference(new UsfmBookCode("JHN"), 3, verseLabel);

        Assert.Equal(verseLabel, reference.VerseLabel);
        Assert.Equal($"JHN 3:{verseLabel}", reference.ToString());
    }

    [Theory]
    [InlineData("a")]
    [InlineData("3..4")]
    [InlineData("3-")]
    public void BibleReference_rejects_an_invalid_verse_label(string verseLabel)
    {
        Assert.Throws<ArgumentException>(() =>
            new BibleReference(new UsfmBookCode("JHN"), 3, verseLabel));
    }

    [Fact]
    public void BibleReference_rejects_a_default_book_code()
    {
        Assert.Throws<ArgumentException>(() =>
            new BibleReference(default, 3, "16"));
    }

    [Fact]
    public void Sha256Digest_normalizes_valid_hexadecimal_text()
    {
        var value = new string('A', 64);

        var digest = new Sha256Digest(value);

        Assert.Equal(new string('a', 64), digest.Value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Sha256Digest_rejects_an_invalid_digest(string value)
    {
        Assert.Throws<ArgumentException>(() => new Sha256Digest(value));
    }
}
