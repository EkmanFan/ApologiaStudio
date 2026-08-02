using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class BibleSupplementalTextConfiguration
    : IEntityTypeConfiguration<BibleSupplementalTextEntity>
{
    public void Configure(EntityTypeBuilder<BibleSupplementalTextEntity> builder)
    {
        builder.ToTable(
            "bible_supplemental_texts",
            table =>
            {
                table.HasCheckConstraint("ck_bible_supplemental_texts_ordinal_positive", "source_ordinal > 0");
                table.HasCheckConstraint("ck_bible_supplemental_texts_marker", "marker IN ('d', 'sp')");
                table.HasCheckConstraint(
                    "ck_bible_supplemental_texts_offset",
                    "(placement = 'Within' AND character_offset >= 0) OR "
                    + "(placement IN ('Before', 'After') AND character_offset IS NULL)");
            });

        builder.HasKey(text => text.Id);

        builder.Property(text => text.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(text => text.VerseId)
            .HasColumnName("verse_id")
            .IsRequired();

        builder.Property(text => text.SourceOrdinal)
            .HasColumnName("source_ordinal")
            .IsRequired();

        builder.Property(text => text.Marker)
            .HasColumnName("marker")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(text => text.Text)
            .HasColumnName("text")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(text => text.Placement)
            .HasColumnName("placement")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(text => text.CharacterOffset)
            .HasColumnName("character_offset");

        builder.HasIndex(text => new { text.VerseId, text.SourceOrdinal })
            .IsUnique()
            .HasDatabaseName("ux_bible_supplemental_texts_verse_ordinal");

        builder.HasOne<BibleVerseEntity>()
            .WithMany()
            .HasForeignKey(text => text.VerseId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
