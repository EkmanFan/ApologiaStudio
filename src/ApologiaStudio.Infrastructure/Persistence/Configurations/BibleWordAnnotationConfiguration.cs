using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class BibleWordAnnotationConfiguration
    : IEntityTypeConfiguration<BibleWordAnnotationEntity>
{
    public void Configure(EntityTypeBuilder<BibleWordAnnotationEntity> builder)
    {
        builder.ToTable(
            "bible_word_annotations",
            table =>
            {
                table.HasCheckConstraint("ck_bible_word_annotations_ordinal_positive", "source_ordinal > 0");
                table.HasCheckConstraint("ck_bible_word_annotations_offset_nonnegative", "character_offset >= 0");
                table.HasCheckConstraint("ck_bible_word_annotations_length_positive", "character_length > 0");
            });

        builder.HasKey(annotation => annotation.Id);

        builder.Property(annotation => annotation.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(annotation => annotation.VerseId)
            .HasColumnName("verse_id")
            .IsRequired();

        builder.Property(annotation => annotation.SourceOrdinal)
            .HasColumnName("source_ordinal")
            .IsRequired();

        builder.Property(annotation => annotation.Marker)
            .HasColumnName("marker")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(annotation => annotation.AttributeName)
            .HasColumnName("attribute_name")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(annotation => annotation.AttributeValue)
            .HasColumnName("attribute_value")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(annotation => annotation.CharacterOffset)
            .HasColumnName("character_offset")
            .IsRequired();

        builder.Property(annotation => annotation.CharacterLength)
            .HasColumnName("character_length")
            .IsRequired();

        builder.HasIndex(annotation => new { annotation.VerseId, annotation.SourceOrdinal })
            .IsUnique()
            .HasDatabaseName("ux_bible_word_annotations_verse_ordinal");

        builder.HasOne<BibleVerseEntity>()
            .WithMany()
            .HasForeignKey(annotation => annotation.VerseId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
