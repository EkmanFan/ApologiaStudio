using ApologiaStudio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ApologiaStudio.UnitTests.Infrastructure.BibleCorpora;

public sealed class BibleCorpusPersistenceModelTests
{
    private static readonly string[] ExpectedTables =
    [
        "bible_editions",
        "bible_corpus_versions",
        "bible_source_artifacts",
        "bible_books",
        "bible_corpus_books",
        "bible_verses",
        "bible_word_annotations",
        "bible_supplemental_texts"
    ];

    [Fact]
    public void Model_ShouldContainTheEightCanonicalBibleTables()
    {
        using var context = CreateContext();

        var actualTables = context.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(tableName => tableName is not null && tableName.StartsWith("bible_", StringComparison.Ordinal))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ExpectedTables.Order(StringComparer.Ordinal).ToArray(),
            actualTables);
    }

    [Fact]
    public void Model_ShouldSeedExactlyTheProtestantSixtySixBookCatalog()
    {
        using var context = CreateContext();

        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var bookEntity = Assert.Single(
            designTimeModel.GetEntityTypes(),
            entityType => entityType.GetTableName() == "bible_books");

        var seed = bookEntity.GetSeedData(providerValues: true);

        Assert.Equal(66, seed.Count());
        Assert.Contains(seed, row => Equals(row["UsfmCode"], "GEN") && Equals(row["CanonicalOrder"], 1));
        Assert.Contains(seed, row => Equals(row["UsfmCode"], "REV") && Equals(row["CanonicalOrder"], 66));
    }

    private static ApologiaStudioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApologiaStudioDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=model-only;Password=model-only")
            .Options;

        return new ApologiaStudioDbContext(options);
    }
}
