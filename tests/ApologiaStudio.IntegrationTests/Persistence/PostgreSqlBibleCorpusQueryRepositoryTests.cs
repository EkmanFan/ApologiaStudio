using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Application.BibleCorpora.Reader;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlBibleCorpusQueryRepositoryTests
{
    [Fact]
    public async Task Repository_ShouldReadOnlyActiveApprovedBibleContent()
    {
        await using var context = CreateContext();

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        await SeedBibleContentAsync(context);

        var repository = new EfBibleCorpusQueryRepository(context);

        var editions = await repository.ListActiveEditionsAsync(
            CancellationToken.None);

        var edition = Assert.Single(editions);
        Assert.Equal("lsg1910", edition.Code);
        Assert.Equal("fr", edition.LanguageTag);

        var editionBooks = await repository.GetBooksAsync(
            new BibleEditionCode("LSG1910"),
            CancellationToken.None);

        Assert.NotNull(editionBooks);
        var book = Assert.Single(editionBooks.Books);
        Assert.Equal("GEN", book.Code);
        Assert.Equal("Genèse", book.DisplayName);
        Assert.Equal(1, book.ChapterCount);

        var chapter = await repository.GetChapterAsync(
            new BibleEditionCode("lsg1910"),
            new UsfmBookCode("GEN"),
            1,
            CancellationToken.None);

        Assert.NotNull(chapter);
        Assert.Equal(1, chapter.Book.ChapterCount);
        Assert.Collection(
            chapter.Verses,
            firstVerse =>
            {
                Assert.Equal("1", firstVerse.VerseLabel);
                Assert.Equal("Au commencement", firstVerse.Text);

                var annotation = Assert.Single(
                    firstVerse.WordAnnotations);

                Assert.Equal("strong", annotation.Name);
                Assert.Equal("H7225", annotation.Value);
            },
            secondVerse =>
            {
                Assert.Equal("2", secondVerse.VerseLabel);
                Assert.Equal("La terre était informe", secondVerse.Text);
                Assert.Empty(secondVerse.WordAnnotations);
            });

        var verse = await repository.GetVerseAsync(
            new BibleEditionCode("lsg1910"),
            new BibleReference(
                new UsfmBookCode("GEN"),
                1,
                "1"),
            CancellationToken.None);

        Assert.NotNull(verse);
        Assert.Equal("Au commencement", verse.Text);
        Assert.Single(verse.WordAnnotations);

        Assert.Null(await repository.GetChapterAsync(
            new BibleEditionCode("lsg1910"),
            new UsfmBookCode("GEN"),
            2,
            CancellationToken.None));

        Assert.Null(await repository.GetBooksAsync(
            new BibleEditionCode("web-classic"),
            CancellationToken.None));

        var reader = await new GetBibleReaderHandler(repository)
            .HandleAsync(
                new GetBibleReaderQuery(
                    "lsg1910",
                    "GEN",
                    1),
                CancellationToken.None);

        Assert.Equal(BibleReaderStatus.Ready, reader.Status);
        Assert.Equal("Au commencement", reader.Chapter!.Verses[0].Text);
        Assert.Null(reader.PreviousChapter);
        Assert.Null(reader.NextChapter);
    }

    private static ApologiaStudioDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_TEST_DB_CONNECTION");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        var options = new DbContextOptionsBuilder<ApologiaStudioDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApologiaStudioDbContext(options);
    }

    private static async Task SeedBibleContentAsync(
        ApologiaStudioDbContext context)
    {
        var corpusVersionId = Guid.Parse(
            "3073e582-938c-4956-813e-4f23a3f83083");

        var importedAt = new DateTimeOffset(
            2026,
            8,
            2,
            20,
            0,
            0,
            TimeSpan.Zero);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO bible_editions
                (code, display_name, language_tag, canon_code)
            VALUES
                ('lsg1910', 'Louis Segond 1910', 'fr', 'protestant-66'),
                ('web-classic', 'World English Bible Classic', 'en', 'protestant-66')
            """);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO bible_corpus_versions
                (id, edition_code, source_tree_sha256, import_fingerprint,
                 parser_name, parser_version, normalization_policy_id,
                 canonical_schema_version, imported_at, approved_at,
                 validation_status, is_active)
            VALUES
                ({corpusVersionId}, 'lsg1910', {new string('a', 64)},
                 {new string('b', 64)}, 'SIL.Machine', '3.9.1',
                 'unicode-nfc-collapse-whitespace-v1', 1, {importedAt},
                 {importedAt}, 'approved', TRUE)
            """);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO bible_corpus_books
                (corpus_version_id, usfm_book_code, book_ordinal,
                 display_name, short_name, source_relative_path)
            VALUES
                ({corpusVersionId}, 'GEN', 1, 'Genèse', 'Gen', '01-GEN.usfm')
            """);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO bible_verses
                (id, corpus_version_id, usfm_book_code, chapter_number,
                 verse_label, verse_ordinal, text, source_relative_path,
                 source_line)
            VALUES
                (101, {corpusVersionId}, 'GEN', 1, '1', 1,
                 'Au commencement', '01-GEN.usfm', 10),
                (102, {corpusVersionId}, 'GEN', 1, '2', 2,
                 'La terre était informe', '01-GEN.usfm', 11)
            """);

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO bible_word_annotations
                (id, verse_id, source_ordinal, marker, attribute_name,
                 attribute_value, character_offset, character_length)
            VALUES
                (201, 101, 1, 'w', 'strong', 'H7225', 0, 2)
            """);
    }
}
