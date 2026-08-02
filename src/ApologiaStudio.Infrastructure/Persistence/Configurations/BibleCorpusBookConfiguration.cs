using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class BibleCorpusBookConfiguration
    : IEntityTypeConfiguration<BibleCorpusBookEntity>
{
    public void Configure(EntityTypeBuilder<BibleCorpusBookEntity> builder)
    {
        builder.ToTable(
            "bible_corpus_books",
            table => table.HasCheckConstraint(
                "ck_bible_corpus_books_book_ordinal_positive",
                "book_ordinal > 0"));

        builder.HasKey(book => new { book.CorpusVersionId, book.UsfmBookCode });

        builder.Property(book => book.CorpusVersionId)
            .HasColumnName("corpus_version_id")
            .HasColumnType("uuid")
            .HasConversion(StronglyTypedIdConverters.BibleCorpusVersionIdConverter);

        builder.Property(book => book.UsfmBookCode)
            .HasColumnName("usfm_book_code")
            .HasMaxLength(4)
            .HasConversion(StronglyTypedIdConverters.UsfmBookCodeConverter);

        builder.Property(book => book.BookOrdinal)
            .HasColumnName("book_ordinal")
            .IsRequired();

        builder.Property(book => book.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(book => book.ShortName)
            .HasColumnName("short_name")
            .HasMaxLength(100);

        builder.Property(book => book.SourceRelativePath)
            .HasColumnName("source_relative_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(book => new { book.CorpusVersionId, book.BookOrdinal })
            .IsUnique()
            .HasDatabaseName("ux_bible_corpus_books_version_ordinal");

        builder.HasOne<BibleCorpusVersionEntity>()
            .WithMany()
            .HasForeignKey(book => book.CorpusVersionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<BibleBookEntity>()
            .WithMany()
            .HasForeignKey(book => book.UsfmBookCode)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
