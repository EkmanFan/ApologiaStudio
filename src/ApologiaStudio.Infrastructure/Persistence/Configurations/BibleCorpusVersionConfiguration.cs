using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using ApologiaStudio.Domain.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class BibleCorpusVersionConfiguration
    : IEntityTypeConfiguration<BibleCorpusVersionEntity>
{
    public void Configure(EntityTypeBuilder<BibleCorpusVersionEntity> builder)
    {
        builder.ToTable(
            "bible_corpus_versions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_bible_corpus_versions_schema_version_positive",
                    "canonical_schema_version > 0");
                table.HasCheckConstraint(
                    "ck_bible_corpus_versions_validation_status",
                    "validation_status IN ('pending', 'validated', 'approved', 'failed')");
                table.HasCheckConstraint(
                    "ck_bible_corpus_versions_source_tree_sha256",
                    "source_tree_sha256 ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_bible_corpus_versions_import_fingerprint",
                    "import_fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_bible_corpus_versions_approval",
                    "(approved_at IS NULL OR approved_at >= imported_at) AND "
                    + "(NOT is_active OR (approved_at IS NOT NULL AND validation_status = 'approved'))");
            });

        builder.HasKey(version => version.Id);

        builder.Property(version => version.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasConversion(StronglyTypedIdConverters.BibleCorpusVersionIdConverter)
            .ValueGeneratedNever();

        builder.Property(version => version.EditionCode)
            .HasColumnName("edition_code")
            .HasMaxLength(64)
            .HasConversion(StronglyTypedIdConverters.BibleEditionCodeConverter)
            .IsRequired();

        builder.Property(version => version.UpstreamRevision)
            .HasColumnName("upstream_revision")
            .HasMaxLength(200);

        ConfigureDigest(builder.Property(version => version.SourceTreeSha256), "source_tree_sha256");
        ConfigureDigest(builder.Property(version => version.ImportFingerprint), "import_fingerprint");

        builder.Property(version => version.ParserName)
            .HasColumnName("parser_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(version => version.ParserVersion)
            .HasColumnName("parser_version")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(version => version.NormalizationPolicyId)
            .HasColumnName("normalization_policy_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(version => version.CanonicalSchemaVersion)
            .HasColumnName("canonical_schema_version")
            .IsRequired();

        builder.Property(version => version.ImportedAt)
            .HasColumnName("imported_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(version => version.ApprovedAt)
            .HasColumnName("approved_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(version => version.ValidationStatus)
            .HasColumnName("validation_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(version => version.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(version => version.ImportFingerprint)
            .IsUnique()
            .HasDatabaseName("ux_bible_corpus_versions_import_fingerprint");

        builder.HasIndex(version => version.EditionCode)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_bible_corpus_versions_active_edition");

        builder.HasIndex(version => new { version.EditionCode, version.ImportedAt })
            .HasDatabaseName("ix_bible_corpus_versions_edition_imported_at");

        builder.HasOne<BibleEditionEntity>()
            .WithMany()
            .HasForeignKey(version => version.EditionCode)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }

    private static void ConfigureDigest(
        PropertyBuilder<Sha256Digest> property,
        string columnName)
    {
        property
            .HasColumnName(columnName)
            .HasColumnType("character(64)")
            .HasConversion(StronglyTypedIdConverters.Sha256DigestConverter)
            .IsRequired();
    }
}
