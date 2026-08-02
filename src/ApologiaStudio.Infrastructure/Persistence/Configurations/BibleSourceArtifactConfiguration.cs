using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class BibleSourceArtifactConfiguration
    : IEntityTypeConfiguration<BibleSourceArtifactEntity>
{
    public void Configure(EntityTypeBuilder<BibleSourceArtifactEntity> builder)
    {
        builder.ToTable(
            "bible_source_artifacts",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_bible_source_artifacts_role",
                    "role IN ('canonical-usfm', 'validation-vpl', 'validation-report')");
                table.HasCheckConstraint(
                    "ck_bible_source_artifacts_byte_length_positive",
                    "byte_length > 0");
                table.HasCheckConstraint(
                    "ck_bible_source_artifacts_sha256",
                    "sha256 ~ '^[0-9a-f]{64}$'");
            });

        builder.HasKey(artifact => artifact.Id);

        builder.Property(artifact => artifact.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();

        builder.Property(artifact => artifact.CorpusVersionId)
            .HasColumnName("corpus_version_id")
            .HasColumnType("uuid")
            .HasConversion(StronglyTypedIdConverters.BibleCorpusVersionIdConverter)
            .IsRequired();

        builder.Property(artifact => artifact.Role)
            .HasColumnName("role")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(artifact => artifact.SourceUri)
            .HasColumnName("source_uri")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(artifact => artifact.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(artifact => artifact.Sha256)
            .HasColumnName("sha256")
            .HasColumnType("character(64)")
            .HasConversion(StronglyTypedIdConverters.Sha256DigestConverter)
            .IsRequired();

        builder.Property(artifact => artifact.ByteLength)
            .HasColumnName("byte_length")
            .IsRequired();

        builder.Property(artifact => artifact.DownloadedAt)
            .HasColumnName("downloaded_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(artifact => new { artifact.CorpusVersionId, artifact.Role, artifact.FileName })
            .IsUnique()
            .HasDatabaseName("ux_bible_source_artifacts_version_role_file");

        builder.HasOne<BibleCorpusVersionEntity>()
            .WithMany()
            .HasForeignKey(artifact => artifact.CorpusVersionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
