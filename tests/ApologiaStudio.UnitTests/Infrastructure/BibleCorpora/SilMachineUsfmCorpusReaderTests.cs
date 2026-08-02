using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;

namespace ApologiaStudio.UnitTests.Infrastructure.BibleCorpora;

public sealed class SilMachineUsfmCorpusReaderTests
{
    [Fact]
    public async Task Reader_returns_canonical_order_metadata_visible_text_and_complete_word_spans()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write(
            "nested/02EXO.usfm",
            """
            \id EXO
            \toc1 Exodus
            \toc2 Exod
            \c 1
            \p
            \v 1 These are the names.
            """);
        fixture.Write(
            "01GEN.usfm",
            """
            \id GEN
            \toc1 Genesis
            \toc2 Gen
            \c 1
            \p
            \v 1 In the \w beginning|strong="H7225" lemma="רֵאשִׁית"\w*, God created.
            """);

        var result = await ReadAsync(fixture.Path);

        Assert.Equal(2, result.SourceFileCount);
        Assert.Collection(
            result.Books,
            genesis =>
            {
                Assert.Equal(new UsfmBookCode("GEN"), genesis.BookCode);
                Assert.Equal(1, genesis.BookOrdinal);
                Assert.Equal("Genesis", genesis.DisplayName);
                Assert.Equal("Gen", genesis.ShortName);
                Assert.Equal("01GEN.usfm", genesis.SourceRelativePath);
                Assert.NotNull(genesis.SourceSha256);
                Assert.True(genesis.SourceByteLength is > 0);
            },
            exodus =>
            {
                Assert.Equal(new UsfmBookCode("EXO"), exodus.BookCode);
                Assert.Equal(2, exodus.BookOrdinal);
                Assert.Equal("nested/02EXO.usfm", exodus.SourceRelativePath);
            });

        var verse = result.Verses[0];
        Assert.Equal(new BibleReference(new UsfmBookCode("GEN"), 1, "1"), verse.Reference);
        Assert.Equal("In the beginning, God created.", verse.Text);
        Assert.Collection(
            verse.WordAnnotations,
            strong =>
            {
                Assert.Equal(1, strong.SourceOrdinal);
                Assert.Equal("strong", strong.Name);
                Assert.Equal("H7225", strong.Value);
                Assert.Equal(7, strong.CharacterOffset);
                Assert.Equal(9, strong.CharacterLength);
            },
            lemma =>
            {
                Assert.Equal(2, lemma.SourceOrdinal);
                Assert.Equal("lemma", lemma.Name);
                Assert.Equal("רֵאשִׁית", lemma.Value);
                Assert.Equal(7, lemma.CharacterOffset);
                Assert.Equal(9, lemma.CharacterLength);
            });
    }

    [Fact]
    public async Task Reader_distinguishes_before_within_and_after_supplemental_text()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write(
            "19PSA.usfm",
            """
            \id PSA
            \h Psalms
            \c 1
            \d A Psalm by David.
            \q1
            \v 1 First verse.
            \d SELAH
            \q1
            \v 2 The king has spoken.
            \sp People
            \q1 We will rejoice.
            """);

        var result = await ReadAsync(fixture.Path);

        var first = Assert.Single(
            result.Verses,
            verse => verse.Reference.VerseLabel == "1");
        Assert.Collection(
            first.SupplementalTexts,
            before =>
            {
                Assert.Equal(BibleSupplementalTextPlacement.Before, before.Placement);
                Assert.Null(before.CharacterOffset);
                Assert.Equal("A Psalm by David.", before.Text);
            },
            after =>
            {
                Assert.Equal(BibleSupplementalTextPlacement.After, after.Placement);
                Assert.Null(after.CharacterOffset);
                Assert.Equal("SELAH", after.Text);
            });

        var second = Assert.Single(
            result.Verses,
            verse => verse.Reference.VerseLabel == "2");
        var within = Assert.Single(second.SupplementalTexts);
        Assert.Equal(BibleSupplementalTextPlacement.Within, within.Placement);
        Assert.Equal("The king has spoken.".Length, within.CharacterOffset!.Value);
        Assert.Equal("People", within.Text);
        Assert.Equal("The king has spoken. We will rejoice.", second.Text);
    }

    [Fact]
    public async Task Reader_does_not_treat_word_attributes_in_supplemental_text_as_verse_annotations()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write(
            "19PSA.usfm",
            """
            \id PSA
            \h Psalms
            \c 119
            \q1
            \v 32 I run in the path of your commandments.
            \d \w HE|strong="H3588"\w*
            \q1
            \v 33 Teach me, LORD, the way of your statutes.
            """);

        var result = await ReadAsync(fixture.Path);

        var verse = Assert.Single(
            result.Verses,
            item => item.Reference.VerseLabel == "32");
        Assert.Empty(verse.WordAnnotations);
        var supplemental = Assert.Single(verse.SupplementalTexts);
        Assert.Equal("HE", supplemental.Text);
        Assert.Equal(BibleSupplementalTextPlacement.After, supplemental.Placement);
    }

    [Fact]
    public async Task Reader_excludes_non_canonical_documents_before_requiring_verses()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write(
            "00FRT.usfm",
            """
            \id FRT
            \h Front Matter
            """);
        fixture.Write(
            "01GEN.usfm",
            """
            \id GEN
            \h Genesis
            \c 1
            \p
            \v 1 In the beginning.
            """);

        var result = await new SilMachineUsfmCorpusReader().ReadAsync(
            new BibleCorpusReadRequest(fixture.Path, [new UsfmBookCode("FRT")]),
            CancellationToken.None);

        Assert.Equal(1, result.SourceFileCount);
        Assert.Single(result.Books);
        Assert.Single(result.Verses);
    }

    [Fact]
    public async Task Reader_rejects_unknown_markers()
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

        var exception = await Assert.ThrowsAsync<BibleCorpusReadException>(() =>
            ReadAsync(fixture.Path));

        Assert.Contains("Unknown paragraph marker", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_rejects_duplicate_references()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write(
            "01GEN.usfm",
            """
            \id GEN
            \c 1
            \p
            \v 1 First declaration.
            \v 1 Second declaration.
            """);

        var exception = await Assert.ThrowsAsync<BibleCorpusReadException>(() =>
            ReadAsync(fixture.Path));

        Assert.Contains("Duplicate USFM reference GEN 1:1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_honors_pre_cancelled_requests()
    {
        using var fixture = CorpusFixture.Create();
        fixture.Write(
            "01GEN.usfm",
            """
            \id GEN
            \c 1
            \p
            \v 1 In the beginning.
            """);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReadAsync(fixture.Path, cancellation.Token));
    }

    private static Task<BibleCorpusReadResult> ReadAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        new SilMachineUsfmCorpusReader().ReadAsync(
            new BibleCorpusReadRequest(path),
            cancellationToken);

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
                $"apologia-usfm-reader-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new CorpusFixture(path);
        }

        public void Write(string relativePath, string content)
        {
            var file = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            File.WriteAllText(file, content);
        }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
