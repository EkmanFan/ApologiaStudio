using ApologiaStudio.BibleCorpusBench;

namespace ApologiaStudio.UnitTests.BibleCorpus;

public sealed class BibleCorpusBenchTests
{
    [Fact]
    public void UsfmReader_extracts_only_visible_verse_text_and_preserves_strong_attributes()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write(
            "01GEN.usfm",
            """
            \id GEN
            \h Genesis
            \c 1
            \s1 Creation
            \p
            \v 1 In the \w beginning|strong="H7225"\w*, God created. \x + \xo 1:1 \xt Job 38:4\x*
            \v 2 The earth was formless.
            """);

        var result = new UsfmCorpusReader().Read(fixture.Path);

        Assert.Equal(1, result.FileCount);
        Assert.Equal(1, result.BookCount);
        Assert.Equal(2, result.Verses.Count);
        Assert.Equal(1, result.StrongAttributeCount);
        Assert.Equal(
            "In the beginning, God created.",
            result.Verses[new VerseKey("GEN", 1, "1")].Text);
        Assert.Equal(
            "H7225",
            Assert.Single(result.Verses[new VerseKey("GEN", 1, "1")].WordAnnotations).Value);
    }

    [Fact]
    public void UsfmReader_rejects_unknown_markers()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write(
            "01GEN.usfm",
            """
            \id GEN
            \c 1
            \p
            \v 1 Valid text.
            \unknown Invalid text.
            """);

        var exception = Assert.Throws<BibleCorpusException>(() =>
            new UsfmCorpusReader().Read(fixture.Path));

        Assert.Contains("Unknown paragraph marker", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VplReader_and_comparer_report_text_differences_without_hiding_punctuation()
    {
        using var usfmFixture = CorpusFixture.Create();
        using var vplFixture = CorpusFixture.Create();
        usfmFixture.Write(
            "01GEN.usfm",
            """
            \id GEN
            \c 1
            \p
            \v 1 In  the beginning.
            """);
        vplFixture.Write("genesis.vpl", "GEN 1:1 In the beginning!\n");

        var usfm = new UsfmCorpusReader().Read(usfmFixture.Path);
        var vpl = new VplCorpusReader().Read(vplFixture.Path);
        var report = new CorpusComparer().Compare("fixture", usfm, vpl, 1, false, 10);

        Assert.False(report.IsMatch);
        Assert.Equal(1, report.TextMismatchCount);
        Assert.Equal("In the beginning.", Assert.Single(report.Differences).UsfmText);
        Assert.Equal("In the beginning!", Assert.Single(report.Differences).VplText);
    }

    [Fact]
    public void Comparer_accepts_unicode_and_whitespace_only_normalization()
    {
        using var usfmFixture = CorpusFixture.Create();
        using var vplFixture = CorpusFixture.Create();
        usfmFixture.Write(
            "01GEN.usfm",
            """
            \id GEN
            \c 1
            \p
            \v 1 Au commencement, Dieu créa.
            """);
        vplFixture.Write("genesis.vpl", "GEN 1:1 Au\u00A0commencement, Dieu cre\u0301a.\n");

        var usfm = new UsfmCorpusReader().Read(usfmFixture.Path);
        var vpl = new VplCorpusReader().Read(vplFixture.Path);
        var report = new CorpusComparer().Compare("fixture", usfm, vpl, 1, false, 10);

        Assert.True(report.IsMatch);
    }

    [Theory]
    [InlineData("1JO", "1JN")]
    [InlineData("2JO", "2JN")]
    [InlineData("3JO", "3JN")]
    [InlineData("EZE", "EZK")]
    [InlineData("JAM", "JAS")]
    [InlineData("JOE", "JOL")]
    [InlineData("JOH", "JHN")]
    [InlineData("MAR", "MRK")]
    [InlineData("NAH", "NAM")]
    [InlineData("PHI", "PHP")]
    [InlineData("SOL", "SNG")]
    public void VplReader_normalizes_BibleWorks_book_aliases_to_Usfm_codes(
        string vplCode,
        string expectedUsfmCode)
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write("book.vpl", $"{vplCode} 1:1 Text.\n");

        var result = new VplCorpusReader().Read(fixture.Path);

        Assert.Contains(new VerseKey(expectedUsfmCode, 1, "1"), result.Verses.Keys);
    }

    [Fact]
    public void VplReader_accepts_an_empty_verse()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write("book.vpl", "LUK 17:36 \n");

        var result = new VplCorpusReader().Read(fixture.Path);

        Assert.Equal(string.Empty, result.Verses[new VerseKey("LUK", 17, "36")].Text);
    }

    [Fact]
    public void Comparer_preserves_a_descriptive_title_separately_and_matches_flattened_Vpl()
    {
        using var usfmFixture = CorpusFixture.Create();
        using var vplFixture = CorpusFixture.Create();
        usfmFixture.Write(
            "19PSA.usfm",
            """
            \id PSA
            \c 3
            \d A Psalm by David.
            \q1
            \v 1 Yahweh, how my adversaries have increased!
            """);
        vplFixture.Write(
            "psalms.vpl",
            "PSA 3:1 A Psalm by David. Yahweh, how my adversaries have increased!\n");

        var usfm = new UsfmCorpusReader().Read(usfmFixture.Path);
        var vpl = new VplCorpusReader().Read(vplFixture.Path);
        var verse = usfm.Verses[new VerseKey("PSA", 3, "1")];
        var report = new CorpusComparer().Compare("fixture", usfm, vpl, 1, false, 10);

        var supplemental = Assert.Single(verse.SupplementalTexts);
        Assert.Equal("d", supplemental.Marker);
        Assert.Equal("A Psalm by David.", supplemental.Text);
        Assert.Equal(0, supplemental.CharacterOffset);
        Assert.False(supplemental.OccurredWithinVerse);
        Assert.Equal("Yahweh, how my adversaries have increased!", verse.Text);
        Assert.True(report.IsMatch);
    }

    [Fact]
    public void Comparer_keeps_a_mid_chapter_descriptive_label_after_the_preceding_verse()
    {
        using var usfmFixture = CorpusFixture.Create();
        using var vplFixture = CorpusFixture.Create();
        usfmFixture.Write(
            "19PSA.usfm",
            """
            \id PSA
            \c 119
            \q1
            \v 104 I hate every false way.
            \d NUN
            \q1
            \v 105 Your word is a lamp to my feet.
            """);
        vplFixture.Write(
            "psalms.vpl",
            """
            PSA 119:104 I hate every false way. NUN
            PSA 119:105 Your word is a lamp to my feet.
            """);

        var usfm = new UsfmCorpusReader().Read(usfmFixture.Path);
        var vpl = new VplCorpusReader().Read(vplFixture.Path);
        var verse = usfm.Verses[new VerseKey("PSA", 119, "104")];
        var report = new CorpusComparer().Compare("fixture", usfm, vpl, 1, false, 10);

        var supplemental = Assert.Single(verse.SupplementalTexts);
        Assert.Equal("d", supplemental.Marker);
        Assert.Equal("NUN", supplemental.Text);
        Assert.Equal(verse.Text.Length, supplemental.CharacterOffset);
        Assert.True(supplemental.OccurredWithinVerse);
        Assert.True(report.IsMatch);
    }

    [Fact]
    public void Comparer_preserves_and_flattens_a_mid_verse_speaker_label()
    {
        using var usfmFixture = CorpusFixture.Create();
        using var vplFixture = CorpusFixture.Create();
        usfmFixture.Write(
            "22SNG.usfm",
            """
            \id SNG
            \c 1
            \p
            \v 4 The king has brought me into his rooms.
            \sp Friends
            \q1 We will be glad and rejoice in you.
            """);
        vplFixture.Write(
            "song.vpl",
            "SNG 1:4 The king has brought me into his rooms. Friends We will be glad and rejoice in you.\n");

        var usfm = new UsfmCorpusReader().Read(usfmFixture.Path);
        var vpl = new VplCorpusReader().Read(vplFixture.Path);
        var verse = usfm.Verses[new VerseKey("SNG", 1, "4")];
        var report = new CorpusComparer().Compare("fixture", usfm, vpl, 1, false, 10);

        var supplemental = Assert.Single(verse.SupplementalTexts);
        Assert.Equal("sp", supplemental.Marker);
        Assert.Equal("Friends", supplemental.Text);
        Assert.True(supplemental.OccurredWithinVerse);
        Assert.True(report.IsMatch);
    }

    [Fact]
    public void Comparer_preserves_but_does_not_flatten_a_speaker_heading_between_verses()
    {
        using var usfmFixture = CorpusFixture.Create();
        using var vplFixture = CorpusFixture.Create();
        usfmFixture.Write(
            "22SNG.usfm",
            """
            \id SNG
            \c 6
            \q1
            \v 3 I am my beloved's.
            \sp Lover
            \q1
            \v 4 You are beautiful, my love.
            """);
        vplFixture.Write(
            "song.vpl",
            """
            SNG 6:3 I am my beloved's.
            SNG 6:4 You are beautiful, my love.
            """);

        var usfm = new UsfmCorpusReader().Read(usfmFixture.Path);
        var vpl = new VplCorpusReader().Read(vplFixture.Path);
        var verse = usfm.Verses[new VerseKey("SNG", 6, "3")];
        var report = new CorpusComparer().Compare("fixture", usfm, vpl, 1, false, 10);

        var supplemental = Assert.Single(verse.SupplementalTexts);
        Assert.Equal("sp", supplemental.Marker);
        Assert.True(supplemental.OccurredWithinVerse);
        Assert.True(report.IsMatch);
    }

    private sealed class CorpusFixture : IDisposable
    {
        private CorpusFixture(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static CorpusFixture Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"apologia-corpus-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new CorpusFixture(path);
        }

        public void Write(string relativePath, string content)
        {
            var file = System.IO.Path.Combine(Path, relativePath);
            File.WriteAllText(file, content);
        }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
