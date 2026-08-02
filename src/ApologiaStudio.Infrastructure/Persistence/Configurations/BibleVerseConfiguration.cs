using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class BibleVerseConfiguration
    : IEntityTypeConfiguration<BibleVerseEntity>
{
    public void Configure(EntityTypeBuilder<BibleVerseEntity> builder)
    {
        builder.ToTable(
            "bible_verses",
            table =>
            {
                table.HasCheckConstraint("ck_bible_verses_chapter_positive", "chapter_number > 0");
                table.HasCheckConstraint("ck_bible_verses_ordinal_positive", "verse_ordinal > 0");
                table.HasCheckConstraint("ck_bible_verses_source_line_positive", "source_line > 0");
            });

        builder.HasKey(verse => verse.Id);

        builder.Property(verse => verse.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(verse => verse.CorpusVersionId)
            .HasColumnName("corpus_version_id")
            .HasColumnType("uuid")
            .HasConversion(StronglyTypedIdConverters.BibleCorpusVersionIdConverter)
            .IsRequired();

        builder.Property(verse => verse.UsfmBookCode)
            .HasColumnName("usfm_book_code")
            .HasMaxLength(4)
            .HasConversion(StronglyTypedIdConverters.UsfmBookCodeConverter)
            .IsRequired();

        builder.Property(verse => verse.ChapterNumber)
            .HasColumnName("chapter_number")
            .IsRequired();

        builder.Property(verse => verse.VerseLabel)
            .HasColumnName("verse_label")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(verse => verse.VerseOrdinal)
            .HasColumnName("verse_ordinal")
            .IsRequired();

        builder.Property(verse => verse.Text)
            .HasColumnName("text")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(verse => verse.SourceRelativePath)
            .HasColumnName("source_relative_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(verse => verse.SourceLine)
            .HasColumnName("source_line")
            .IsRequired();

        builder.HasIndex(verse => new
            {
                verse.CorpusVersionId,
                verse.UsfmBookCode,
                verse.ChapterNumber,
                verse.VerseLabel
            })
            .IsUnique()
            .HasDatabaseName("ux_bible_verses_reference");

        builder.HasIndex(verse => new
            {
                verse.CorpusVersionId,
                verse.UsfmBookCode,
                verse.ChapterNumber,
                verse.VerseOrdinal
            })
            .IsUnique()
            .HasDatabaseName("ux_bible_verses_order");

        builder.HasOne<BibleCorpusBookEntity>()
            .WithMany()
            .HasForeignKey(verse => new { verse.CorpusVersionId, verse.UsfmBookCode })
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
