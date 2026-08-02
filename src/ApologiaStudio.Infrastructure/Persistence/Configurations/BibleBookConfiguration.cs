using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class BibleBookConfiguration
    : IEntityTypeConfiguration<BibleBookEntity>
{
    public void Configure(EntityTypeBuilder<BibleBookEntity> builder)
    {
        builder.ToTable(
            "bible_books",
            table => table.HasCheckConstraint(
                "ck_bible_books_canonical_order_positive",
                "canonical_order > 0"));

        builder.HasKey(book => book.UsfmCode);

        builder.Property(book => book.UsfmCode)
            .HasColumnName("usfm_code")
            .HasMaxLength(4)
            .HasConversion(StronglyTypedIdConverters.UsfmBookCodeConverter)
            .ValueGeneratedNever();

        builder.Property(book => book.OsisCode)
            .HasColumnName("osis_code")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(book => book.CanonicalOrder)
            .HasColumnName("canonical_order")
            .IsRequired();

        builder.Property(book => book.CanonCode)
            .HasColumnName("canon_code")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(book => book.OsisCode)
            .IsUnique()
            .HasDatabaseName("ux_bible_books_osis_code");

        builder.HasIndex(book => book.CanonicalOrder)
            .IsUnique()
            .HasDatabaseName("ux_bible_books_canonical_order");

        builder.HasData(BibleBookSeed.All);
    }
}
