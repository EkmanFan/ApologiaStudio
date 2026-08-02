using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class BibleEditionConfiguration
    : IEntityTypeConfiguration<BibleEditionEntity>
{
    public void Configure(EntityTypeBuilder<BibleEditionEntity> builder)
    {
        builder.ToTable("bible_editions");

        builder.HasKey(edition => edition.Code);

        builder.Property(edition => edition.Code)
            .HasColumnName("code")
            .HasMaxLength(64)
            .HasConversion(StronglyTypedIdConverters.BibleEditionCodeConverter)
            .ValueGeneratedNever();

        builder.Property(edition => edition.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(edition => edition.LanguageTag)
            .HasColumnName("language_tag")
            .HasMaxLength(35)
            .IsRequired();

        builder.Property(edition => edition.CanonCode)
            .HasColumnName("canon_code")
            .HasMaxLength(64)
            .IsRequired();
    }
}
