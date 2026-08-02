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
