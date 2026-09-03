using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge;

internal static class KnowledgeModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureResource(modelBuilder.Entity<KnowledgeResourceEntity>());
        ConfigureWork(modelBuilder.Entity<KnowledgeWorkEntity>());
        ConfigureExpression(modelBuilder.Entity<KnowledgeExpressionEntity>());
        ConfigureExpressionRelation(modelBuilder.Entity<KnowledgeExpressionRelationEntity>());
        ConfigureManifestation(modelBuilder.Entity<KnowledgeManifestationEntity>());
        ConfigureManifestationIdentifier(modelBuilder.Entity<KnowledgeManifestationIdentifierEntity>());
        ConfigureContributor(modelBuilder.Entity<KnowledgeContributorEntity>());
        ConfigureContributorIdentifier(modelBuilder.Entity<KnowledgeContributorIdentifierEntity>());
        ConfigureContribution(modelBuilder.Entity<KnowledgeContributionEntity>());
        ConfigureArtifact(modelBuilder.Entity<KnowledgeArtifactEntity>());
        ConfigureProcessingActivity(modelBuilder.Entity<KnowledgeProcessingActivityEntity>());
        ConfigureDocumentSegment(modelBuilder.Entity<KnowledgeDocumentSegmentEntity>());
        ConfigureRetrievalChunk(modelBuilder.Entity<KnowledgeRetrievalChunkEntity>());
        ConfigureChunkSegment(modelBuilder.Entity<KnowledgeRetrievalChunkSegmentEntity>());
        ConfigureChunkEmbedding(modelBuilder.Entity<KnowledgeChunkEmbeddingEntity>());
        ConfigureDocumentManagerResult(
            modelBuilder.Entity<DocumentManagerResultInboxEntity>());
        ConfigureDocumentManagerVisualAsset(
            modelBuilder.Entity<DocumentManagerVisualAssetInboxEntity>());
        ConfigureDocumentManagerSubmissionManifest(
            modelBuilder.Entity<DocumentManagerSubmissionManifestInboxEntity>());
        ConfigureDocumentManagerExpectedUnit(
            modelBuilder.Entity<DocumentManagerExpectedUnitInboxEntity>());
        ConfigureDocumentManagerEditorialDraft(
            modelBuilder.Entity<DocumentManagerEditorialDraftEntity>());
        ConfigureDocumentManagerEditorialDraftPart(
            modelBuilder.Entity<DocumentManagerEditorialDraftPartEntity>());
        ConfigureDocumentManagerEditorialReviewEvent(
            modelBuilder.Entity<DocumentManagerEditorialReviewEventEntity>());
        ConfigureMetadataAssertion(modelBuilder.Entity<KnowledgeMetadataAssertionEntity>());
        ConfigureSourceKind(modelBuilder.Entity<KnowledgeSourceKindEntity>());
        ConfigureSourceKindAssertion(modelBuilder.Entity<KnowledgeSourceKindAssertionEntity>());
        ConfigurePerspective(modelBuilder.Entity<KnowledgePerspectiveEntity>());
        ConfigurePerspectiveAssertion(modelBuilder.Entity<KnowledgePerspectiveAssertionEntity>());
        ConfigureMethodologicalFramework(
            modelBuilder.Entity<KnowledgeMethodologicalFrameworkEntity>());
        ConfigureMethodologicalFrameworkAssertion(
            modelBuilder.Entity<KnowledgeMethodologicalFrameworkAssertionEntity>());
        ConfigureEpistemicFramework(
            modelBuilder.Entity<KnowledgeEpistemicFrameworkEntity>());
        ConfigureEpistemicFrameworkAssertion(
            modelBuilder.Entity<KnowledgeEpistemicFrameworkAssertionEntity>());
        ConfigureEvidenceRole(modelBuilder.Entity<KnowledgeEvidenceRoleEntity>());
        ConfigureEvidenceRoleAssertion(modelBuilder.Entity<KnowledgeEvidenceRoleAssertionEntity>());
        ConfigureGenreFormAuthoritySnapshot(
            modelBuilder.Entity<GenreFormAuthoritySnapshotEntity>());
        ConfigureGenreFormAuthorityTerm(
            modelBuilder.Entity<GenreFormAuthorityTermEntity>());
        ConfigureGenreFormAuthorityVariant(
            modelBuilder.Entity<GenreFormAuthorityVariantEntity>());
        ConfigureGenreFormAuthorityNote(
            modelBuilder.Entity<GenreFormAuthorityNoteEntity>());
        ConfigureGenreFormBroaderRelation(
            modelBuilder.Entity<GenreFormBroaderRelationEntity>());
        ConfigureGenreFormRelatedRelation(
            modelBuilder.Entity<GenreFormRelatedRelationEntity>());
        ConfigureGenreFormProfileEntry(
            modelBuilder.Entity<GenreFormProfileEntryEntity>());
        ConfigureKnowledgeWorkGenreForm(
            modelBuilder.Entity<KnowledgeWorkGenreFormEntity>());
    }

    private static void ConfigureDocumentManagerResult(
        EntityTypeBuilder<DocumentManagerResultInboxEntity> builder)
    {
        builder.ToTable(
            "document_manager_result_inbox",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_document_manager_result_inbox_length",
                    "byte_length > 0");
                table.HasCheckConstraint(
                    "ck_document_manager_result_inbox_sha256",
                    "sha256 ~ '^[0-9a-f]{64}$'");
            });

        builder.HasKey(x => x.ResultReference);
        builder.Property(x => x.ResultReference)
            .HasColumnName("result_reference")
            .HasMaxLength(255);
        builder.Property(x => x.SubmissionId)
            .HasColumnName("submission_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.ProcessingUnitId)
            .HasColumnName("processing_unit_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.ScopeKind)
            .HasColumnName("scope_kind")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.StartPhysicalPageNumber)
            .HasColumnName("start_physical_page_number");
        builder.Property(x => x.EndPhysicalPageNumber)
            .HasColumnName("end_physical_page_number");
        builder.Property(x => x.ScopeTitle)
            .HasColumnName("scope_title")
            .HasMaxLength(500);
        builder.Property(x => x.StartContentUnitIndex)
            .HasColumnName("start_content_unit_index");
        builder.Property(x => x.StartContentUnitId)
            .HasColumnName("start_content_unit_id")
            .HasMaxLength(1024);
        builder.Property(x => x.EndContentUnitIndex)
            .HasColumnName("end_content_unit_index");
        builder.Property(x => x.EndContentUnitId)
            .HasColumnName("end_content_unit_id")
            .HasMaxLength(1024);
        builder.Property(x => x.SchemaVersion)
            .HasColumnName("schema_version")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.ByteLength)
            .HasColumnName("byte_length")
            .IsRequired();
        builder.Property(x => x.Sha256)
            .HasColumnName("sha256")
            .HasColumnType("character(64)")
            .IsRequired();
        builder.Property(x => x.AvailableAtUtc)
            .HasColumnName("available_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(x => x.ReceivedAtUtc)
            .HasColumnName("received_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasIndex(x => x.ReceivedAtUtc)
            .HasDatabaseName("ix_document_manager_result_inbox_received");
        builder.HasIndex(x => x.ProcessingUnitId)
            .HasDatabaseName("ix_document_manager_result_inbox_processing_unit");
    }

    private static void ConfigureDocumentManagerVisualAsset(
        EntityTypeBuilder<DocumentManagerVisualAssetInboxEntity> builder)
    {
        builder.ToTable(
            "document_manager_visual_asset_inbox",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_document_manager_visual_asset_inbox_length",
                    "byte_length > 0");
                table.HasCheckConstraint(
                    "ck_document_manager_visual_asset_inbox_sha256",
                    "sha256 ~ '^[0-9a-f]{64}$'");
            });

        builder.HasKey(x => new { x.ResultReference, x.AssetId });
        builder.Property(x => x.ResultReference)
            .HasColumnName("result_reference")
            .HasMaxLength(255);
        builder.Property(x => x.AssetId)
            .HasColumnName("asset_id")
            .HasMaxLength(512);
        builder.Property(x => x.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.ByteLength)
            .HasColumnName("byte_length")
            .IsRequired();
        builder.Property(x => x.Sha256)
            .HasColumnName("sha256")
            .HasColumnType("character(64)")
            .IsRequired();
        builder.Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasOne(x => x.Result)
            .WithMany(x => x.VisualAssets)
            .HasForeignKey(x => x.ResultReference)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }

    private static void ConfigureDocumentManagerSubmissionManifest(
        EntityTypeBuilder<DocumentManagerSubmissionManifestInboxEntity> builder)
    {
        builder.ToTable(
            "document_manager_submission_manifest_inbox",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_document_manager_submission_manifest_revision",
                    "revision > 0");
                table.HasCheckConstraint(
                    "ck_document_manager_submission_manifest_sha256",
                    "source_sha256 ~ '^[0-9a-f]{64}$'");
            });

        builder.HasKey(x => new { x.SubmissionId, x.Revision });
        builder.Property(x => x.SubmissionId)
            .HasColumnName("submission_id")
            .HasColumnType("uuid");
        builder.Property(x => x.Revision)
            .HasColumnName("revision");
        builder.Property(x => x.SourceSha256)
            .HasColumnName("source_sha256")
            .HasColumnType("character(64)")
            .IsRequired();
        builder.Property(x => x.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(1024)
            .IsRequired();
        builder.Property(x => x.FinalizedAtUtc)
            .HasColumnName("finalized_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.SubmissionId, x.Revision })
            .IsDescending(false, true)
            .HasDatabaseName("ix_document_manager_submission_manifest_latest");
    }

    private static void ConfigureDocumentManagerExpectedUnit(
        EntityTypeBuilder<DocumentManagerExpectedUnitInboxEntity> builder)
    {
        builder.ToTable(
            "document_manager_expected_unit_inbox",
            table => table.HasCheckConstraint(
                "ck_document_manager_expected_unit_ordinal",
                "ordinal > 0"));

        builder.HasKey(
            x => new
            {
                x.SubmissionId,
                x.ManifestRevision,
                x.ProcessingUnitId
            });
        builder.Property(x => x.SubmissionId)
            .HasColumnName("submission_id")
            .HasColumnType("uuid");
        builder.Property(x => x.ManifestRevision)
            .HasColumnName("manifest_revision");
        builder.Property(x => x.ProcessingUnitId)
            .HasColumnName("processing_unit_id")
            .HasColumnType("uuid");
        builder.Property(x => x.Ordinal)
            .HasColumnName("ordinal");
        builder.Property(x => x.ScopeKind)
            .HasColumnName("scope_kind")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.StartPhysicalPageNumber)
            .HasColumnName("start_physical_page_number");
        builder.Property(x => x.EndPhysicalPageNumber)
            .HasColumnName("end_physical_page_number");
        builder.Property(x => x.ScopeTitle)
            .HasColumnName("scope_title")
            .HasMaxLength(500);
        builder.Property(x => x.StartContentUnitIndex)
            .HasColumnName("start_content_unit_index");
        builder.Property(x => x.StartContentUnitId)
            .HasColumnName("start_content_unit_id")
            .HasMaxLength(1024);
        builder.Property(x => x.EndContentUnitIndex)
            .HasColumnName("end_content_unit_index");
        builder.Property(x => x.EndContentUnitId)
            .HasColumnName("end_content_unit_id")
            .HasMaxLength(1024);

        builder.HasIndex(x => new { x.SubmissionId, x.ManifestRevision, x.Ordinal })
            .IsUnique()
            .HasDatabaseName("ux_document_manager_expected_unit_ordinal");

        builder.HasOne(x => x.Manifest)
            .WithMany(x => x.ExpectedUnits)
            .HasForeignKey(x => new { x.SubmissionId, x.ManifestRevision })
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }

    private static void ConfigureDocumentManagerEditorialDraft(
        EntityTypeBuilder<DocumentManagerEditorialDraftEntity> builder)
    {
        builder.ToTable(
            "document_manager_editorial_drafts",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_revision",
                    "manifest_revision > 0");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_sha256",
                    "source_sha256 ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_status",
                    "status IN ('pending_review', 'in_review', 'approved', 'rejected')");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_title_origin",
                    "title_origin IN ('original_filename', 'imported', 'ai_proposed', 'editorial')");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_publication_year",
                    "publication_year IS NULL OR publication_year BETWEEN 1 AND 9999");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_version",
                    "version >= 0");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_update_time",
                    "updated_at_utc >= created_at_utc");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_contributor",
                    "(primary_contributor_name IS NULL) = (primary_contributor_role IS NULL)");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_review_decision",
                    "((status IN ('approved', 'rejected')) AND reviewed_by_user_id IS NOT NULL AND reviewed_at_utc IS NOT NULL) OR ((status IN ('pending_review', 'in_review')) AND reviewed_by_user_id IS NULL AND reviewed_at_utc IS NULL)");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_draft_rejection",
                    "(status = 'rejected' AND rejection_reason IS NOT NULL) OR (status <> 'rejected' AND rejection_reason IS NULL)");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.SubmissionId)
            .HasColumnName("submission_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.ManifestRevision)
            .HasColumnName("manifest_revision")
            .IsRequired();
        builder.Property(x => x.SourceSha256)
            .HasColumnName("source_sha256")
            .HasColumnType("character(64)")
            .IsRequired();
        builder.Property(x => x.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(1024)
            .IsRequired();
        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(x => x.TitleOrigin)
            .HasColumnName("title_origin")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.PrimaryContributorName)
            .HasColumnName("primary_contributor_name")
            .HasMaxLength(500);
        builder.Property(x => x.PrimaryContributorRole)
            .HasColumnName("primary_contributor_role")
            .HasMaxLength(64);
        builder.Property(x => x.LanguageCode)
            .HasColumnName("language_code")
            .HasMaxLength(35);
        builder.Property(x => x.EditionStatement)
            .HasColumnName("edition_statement")
            .HasMaxLength(500);
        builder.Property(x => x.PublicationYear)
            .HasColumnName("publication_year");
        builder.Property(x => x.PublicationPlace)
            .HasColumnName("publication_place")
            .HasMaxLength(500);
        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
        builder.Property(x => x.LastEditedByUserId)
            .HasColumnName("last_edited_by_user_id")
            .HasColumnType("uuid");
        builder.Property(x => x.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id")
            .HasColumnType("uuid");
        builder.Property(x => x.ReviewedAtUtc)
            .HasColumnName("reviewed_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(x => x.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(4000);
        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => new { x.SubmissionId, x.ManifestRevision })
            .IsUnique()
            .HasDatabaseName("ux_document_manager_editorial_draft_manifest");
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc })
            .HasDatabaseName("ix_document_manager_editorial_draft_review_queue");

        builder.HasOne(x => x.Manifest)
            .WithMany()
            .HasForeignKey(x => new { x.SubmissionId, x.ManifestRevision })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }

    private static void ConfigureDocumentManagerEditorialReviewEvent(
        EntityTypeBuilder<DocumentManagerEditorialReviewEventEntity> builder)
    {
        builder.ToTable(
            "document_manager_editorial_review_events",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_review_event_version",
                    "version > 0");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_review_event_action",
                    "action IN ('save', 'approve', 'reject', 'reopen')");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_review_event_from_status",
                    "from_status IN ('pending_review', 'in_review', 'rejected')");
                table.HasCheckConstraint(
                    "ck_document_manager_editorial_review_event_to_status",
                    "to_status IN ('pending_review', 'in_review', 'approved', 'rejected')");
            });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.DraftId)
            .HasColumnName("draft_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.Version)
            .HasColumnName("version")
            .IsRequired();
        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(x => x.FromStatus)
            .HasColumnName("from_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.ToStatus)
            .HasColumnName("to_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.ActorUserId)
            .HasColumnName("actor_user_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(x => x.SnapshotJson)
            .HasColumnName("snapshot_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(x => new { x.DraftId, x.Version })
            .IsUnique()
            .HasDatabaseName("ux_document_manager_editorial_review_event_version");

        builder.HasOne(x => x.Draft)
            .WithMany(x => x.ReviewEvents)
            .HasForeignKey(x => x.DraftId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }

    private static void ConfigureDocumentManagerEditorialDraftPart(
        EntityTypeBuilder<DocumentManagerEditorialDraftPartEntity> builder)
    {
        builder.ToTable(
            "document_manager_editorial_draft_parts",
            table => table.HasCheckConstraint(
                "ck_document_manager_editorial_draft_part_ordinal",
                "ordinal > 0"));

        builder.HasKey(x => new { x.DraftId, x.ProcessingUnitId });
        builder.Property(x => x.DraftId)
            .HasColumnName("draft_id")
            .HasColumnType("uuid");
        builder.Property(x => x.ProcessingUnitId)
            .HasColumnName("processing_unit_id")
            .HasColumnType("uuid");
        builder.Property(x => x.Ordinal)
            .HasColumnName("ordinal");
        builder.Property(x => x.ResultReference)
            .HasColumnName("result_reference")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(x => x.ScopeKind)
            .HasColumnName("scope_kind")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.StartPhysicalPageNumber)
            .HasColumnName("start_physical_page_number");
        builder.Property(x => x.EndPhysicalPageNumber)
            .HasColumnName("end_physical_page_number");
        builder.Property(x => x.ScopeTitle)
            .HasColumnName("scope_title")
            .HasMaxLength(500);
        builder.Property(x => x.StartContentUnitIndex)
            .HasColumnName("start_content_unit_index");
        builder.Property(x => x.StartContentUnitId)
            .HasColumnName("start_content_unit_id")
            .HasMaxLength(1024);
        builder.Property(x => x.EndContentUnitIndex)
            .HasColumnName("end_content_unit_index");
        builder.Property(x => x.EndContentUnitId)
            .HasColumnName("end_content_unit_id")
            .HasMaxLength(1024);

        builder.HasIndex(x => new { x.DraftId, x.Ordinal })
            .IsUnique()
            .HasDatabaseName("ux_document_manager_editorial_draft_part_ordinal");
        builder.HasIndex(x => x.ResultReference)
            .HasDatabaseName("ix_document_manager_editorial_draft_part_result");

        builder.HasOne(x => x.Draft)
            .WithMany(x => x.Parts)
            .HasForeignKey(x => x.DraftId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne(x => x.Result)
            .WithMany()
            .HasForeignKey(x => x.ResultReference)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }

    private static void ConfigureResource(EntityTypeBuilder<KnowledgeResourceEntity> builder)
    {
        builder.ToTable(
            "knowledge_resources",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_resources_review",
                    "editorial_review_status IN ('pending', 'in_review', 'approved', 'rejected')");
            });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(x => x.EditorialReviewStatus)
            .HasColumnName("editorial_review_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(x => x.EditorialReviewStatus)
            .HasDatabaseName("ix_knowledge_resources_review");
    }

    private static void ConfigureWork(EntityTypeBuilder<KnowledgeWorkEntity> builder)
    {
        builder.ToTable("knowledge_works");
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(x => x.OriginalLanguage)
            .HasColumnName("original_language")
            .HasMaxLength(32);
        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.HasOne<KnowledgeResourceEntity>()
            .WithOne()
            .HasForeignKey<KnowledgeWorkEntity>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }

    private static void ConfigureExpression(EntityTypeBuilder<KnowledgeExpressionEntity> builder)
    {
        builder.ToTable("knowledge_expressions");
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));

        builder.Property(x => x.WorkId)
            .HasColumnName("work_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.LanguageCode)
            .HasColumnName("language_code")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.Label)
            .HasColumnName("label")
            .HasMaxLength(500);
        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.HasOne<KnowledgeResourceEntity>()
            .WithOne()
            .HasForeignKey<KnowledgeExpressionEntity>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeWorkEntity>()
            .WithMany()
            .HasForeignKey(x => x.WorkId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => new { x.WorkId, x.LanguageCode })
            .HasDatabaseName("ix_knowledge_expressions_work_language");
    }

    private static void ConfigureExpressionRelation(
        EntityTypeBuilder<KnowledgeExpressionRelationEntity> builder)
    {
        builder.ToTable(
            "knowledge_expression_relations",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_expr_rel_distinct",
                    "from_expression_id <> to_expression_id");
                table.HasCheckConstraint(
                    "ck_knowledge_expr_rel_type",
                    "relation_type IN ('translation_of', 'revision_of', 'adaptation_of', 'derived_from')");
            });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();
        builder.Property(x => x.FromExpressionId)
            .HasColumnName("from_expression_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.ToExpressionId)
            .HasColumnName("to_expression_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.RelationType)
            .HasColumnName("relation_type")
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<KnowledgeExpressionEntity>()
            .WithMany()
            .HasForeignKey(x => x.FromExpressionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeExpressionEntity>()
            .WithMany()
            .HasForeignKey(x => x.ToExpressionId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => new { x.FromExpressionId, x.ToExpressionId, x.RelationType })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_expression_relations");
    }

    private static void ConfigureManifestation(EntityTypeBuilder<KnowledgeManifestationEntity> builder)
    {
        builder.ToTable(
            "knowledge_manifestations",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_manifestation_year",
                    "publication_year IS NULL OR publication_year BETWEEN 1 AND 9999");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));

        builder.Property(x => x.ExpressionId)
            .HasColumnName("expression_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.EditionStatement)
            .HasColumnName("edition_statement")
            .HasMaxLength(500);
        builder.Property(x => x.PublicationYear)
            .HasColumnName("publication_year");
        builder.Property(x => x.PublicationPlace)
            .HasColumnName("publication_place")
            .HasMaxLength(255);
        builder.Property(x => x.CitationLabel)
            .HasColumnName("citation_label")
            .HasMaxLength(500);

        builder.HasOne<KnowledgeResourceEntity>()
            .WithOne()
            .HasForeignKey<KnowledgeManifestationEntity>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeExpressionEntity>()
            .WithMany()
            .HasForeignKey(x => x.ExpressionId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => x.ExpressionId)
            .HasDatabaseName("ix_knowledge_manifestations_expression");
    }

    private static void ConfigureManifestationIdentifier(
        EntityTypeBuilder<KnowledgeManifestationIdentifierEntity> builder)
    {
        builder.ToTable("knowledge_manifestation_identifiers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();
        builder.Property(x => x.ManifestationId)
            .HasColumnName("manifestation_id")
            .HasColumnType("uuid")
            .IsRequired();
        ConfigureIdentifier(
            builder.Property(x => x.Scheme),
            builder.Property(x => x.Value),
            builder.Property(x => x.Uri));

        builder.HasOne<KnowledgeManifestationEntity>()
            .WithMany()
            .HasForeignKey(x => x.ManifestationId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(x => new { x.ManifestationId, x.Scheme, x.Value })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_manifestation_identifier");
    }

    private static void ConfigureContributor(EntityTypeBuilder<KnowledgeContributorEntity> builder)
    {
        builder.ToTable(
            "knowledge_contributors",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_contributor_type",
                    "contributor_type IN ('person', 'collective_body')");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));

        builder.Property(x => x.ContributorType)
            .HasColumnName("contributor_type")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.PreferredName)
            .HasColumnName("preferred_name")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(x => x.SortName)
            .HasColumnName("sort_name")
            .HasMaxLength(500);
        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.HasOne<KnowledgeResourceEntity>()
            .WithOne()
            .HasForeignKey<KnowledgeContributorEntity>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(x => x.PreferredName)
            .HasDatabaseName("ix_knowledge_contributors_preferred_name");
    }

    private static void ConfigureContributorIdentifier(
        EntityTypeBuilder<KnowledgeContributorIdentifierEntity> builder)
    {
        builder.ToTable("knowledge_contributor_identifiers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();
        builder.Property(x => x.ContributorId)
            .HasColumnName("contributor_id")
            .HasColumnType("uuid")
            .IsRequired();
        ConfigureIdentifier(
            builder.Property(x => x.Scheme),
            builder.Property(x => x.Value),
            builder.Property(x => x.Uri));

        builder.HasOne<KnowledgeContributorEntity>()
            .WithMany()
            .HasForeignKey(x => x.ContributorId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(x => new { x.ContributorId, x.Scheme, x.Value })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_contributor_identifier");
    }

    private static void ConfigureContribution(EntityTypeBuilder<KnowledgeContributionEntity> builder)
    {
        builder.ToTable(
            "knowledge_contributions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_contribution_target",
                    "(CASE WHEN work_id IS NULL THEN 0 ELSE 1 END + " +
                    "CASE WHEN expression_id IS NULL THEN 0 ELSE 1 END + " +
                    "CASE WHEN manifestation_id IS NULL THEN 0 ELSE 1 END) = 1");
                table.HasCheckConstraint(
                    "ck_knowledge_contribution_role",
                    "role IN ('author', 'corporate_author', 'compiler', 'issuing_body', " +
                    "'translator', 'reviser', 'textual_editor', 'transcriber', 'commentator', " +
                    "'publisher', 'series_editor', 'distributor', 'producer')");
                table.HasCheckConstraint(
                    "ck_knowledge_contribution_attribution",
                    "attribution_status IN ('explicit', 'established', 'traditional', 'probable', 'possible', 'disputed')");
                table.HasCheckConstraint(
                    "ck_knowledge_contribution_ordinal",
                    "ordinal >= 0");
            });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();
        builder.Property(x => x.ContributorId)
            .HasColumnName("contributor_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.WorkId)
            .HasColumnName("work_id")
            .HasColumnType("uuid");
        builder.Property(x => x.ExpressionId)
            .HasColumnName("expression_id")
            .HasColumnType("uuid");
        builder.Property(x => x.ManifestationId)
            .HasColumnName("manifestation_id")
            .HasColumnType("uuid");
        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.AttributionStatus)
            .HasColumnName("attribution_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.Ordinal)
            .HasColumnName("ordinal")
            .IsRequired();

        builder.HasOne<KnowledgeContributorEntity>()
            .WithMany()
            .HasForeignKey(x => x.ContributorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<KnowledgeWorkEntity>()
            .WithMany()
            .HasForeignKey(x => x.WorkId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<KnowledgeExpressionEntity>()
            .WithMany()
            .HasForeignKey(x => x.ExpressionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<KnowledgeManifestationEntity>()
            .WithMany()
            .HasForeignKey(x => x.ManifestationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ContributorId)
            .HasDatabaseName("ix_knowledge_contributions_contributor");
    }

    private static void ConfigureArtifact(EntityTypeBuilder<KnowledgeArtifactEntity> builder)
    {
        builder.ToTable(
            "knowledge_artifacts",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_artifact_type",
                    "artifact_type IN ('raw', 'ocr', 'parsed', 'normalized')");
                table.HasCheckConstraint(
                    "ck_knowledge_artifact_sha256",
                    "sha256 ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_knowledge_artifact_length",
                    "byte_length >= 0");
                table.HasCheckConstraint(
                    "ck_knowledge_artifact_lifecycle",
                    "lifecycle_status IN ('active', 'superseded', 'retired', 'corrupted', 'deleted')");
                table.HasCheckConstraint(
                    "ck_knowledge_artifact_derivation",
                    "derived_from_artifact_id IS NULL OR derived_from_artifact_id <> id");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));

        builder.Property(x => x.ManifestationId)
            .HasColumnName("manifestation_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.DerivedFromArtifactId)
            .HasColumnName("derived_from_artifact_id")
            .HasColumnType("uuid");
        builder.Property(x => x.ArtifactType)
            .HasColumnName("artifact_type")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.Sha256)
            .HasColumnName("sha256")
            .HasColumnType("character(64)")
            .IsRequired();
        builder.Property(x => x.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.ByteLength)
            .HasColumnName("byte_length")
            .IsRequired();
        builder.Property(x => x.OriginUri)
            .HasColumnName("origin_uri")
            .HasMaxLength(2048);
        builder.Property(x => x.AcquiredAt)
            .HasColumnName("acquired_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(x => x.LifecycleStatus)
            .HasColumnName("lifecycle_status")
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<KnowledgeResourceEntity>()
            .WithOne()
            .HasForeignKey<KnowledgeArtifactEntity>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeManifestationEntity>()
            .WithMany()
            .HasForeignKey(x => x.ManifestationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<KnowledgeArtifactEntity>()
            .WithMany()
            .HasForeignKey(x => x.DerivedFromArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Sha256)
            .HasDatabaseName("ix_knowledge_artifacts_sha256");
        builder.HasIndex(x => x.DerivedFromArtifactId)
            .HasDatabaseName("ix_knowledge_artifacts_derived_from");
    }

    private static void ConfigureProcessingActivity(
        EntityTypeBuilder<KnowledgeProcessingActivityEntity> builder)
    {
        builder.ToTable(
            "knowledge_processing_activities",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_processing_type",
                    "activity_type IN ('download', 'ocr', 'parse', 'normalize', 'correct')");
                table.HasCheckConstraint(
                    "ck_knowledge_processing_status",
                    "status IN ('pending', 'completed', 'failed')");
                table.HasCheckConstraint(
                    "ck_knowledge_processing_artifacts",
                    "input_artifact_id IS NULL OR input_artifact_id <> output_artifact_id");
                table.HasCheckConstraint(
                    "ck_knowledge_processing_time",
                    "completed_at IS NULL OR completed_at >= started_at");
            });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .UseIdentityByDefaultColumn();
        builder.Property(x => x.InputArtifactId)
            .HasColumnName("input_artifact_id")
            .HasColumnType("uuid");
        builder.Property(x => x.OutputArtifactId)
            .HasColumnName("output_artifact_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.ActivityType)
            .HasColumnName("activity_type")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.ToolName)
            .HasColumnName("tool_name")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.ToolVersion)
            .HasColumnName("tool_version")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.ConfigurationJson)
            .HasColumnName("configuration_json")
            .HasColumnType("jsonb");
        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(x => x.ExecutedBy)
            .HasColumnName("executed_by")
            .HasMaxLength(255);
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<KnowledgeArtifactEntity>()
            .WithMany()
            .HasForeignKey(x => x.InputArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeArtifactEntity>()
            .WithMany()
            .HasForeignKey(x => x.OutputArtifactId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => x.OutputArtifactId)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_processing_output");
    }

    private static void ConfigureDocumentSegment(
        EntityTypeBuilder<KnowledgeDocumentSegmentEntity> builder)
    {
        builder.ToTable(
            "knowledge_document_segments",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_segment_ordinal",
                    "ordinal >= 0");
                table.HasCheckConstraint(
                    "ck_knowledge_segment_parent",
                    "parent_segment_id IS NULL OR parent_segment_id <> id");
                table.HasCheckConstraint(
                    "ck_knowledge_segment_kind",
                    "segment_kind IN ('unknown', 'main_text', 'pedagogical_prompt', 'sidebar', " +
                    "'bibliography', 'caption', 'glossary', 'index')");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));

        builder.Property(x => x.ArtifactId)
            .HasColumnName("artifact_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.ParentSegmentId)
            .HasColumnName("parent_segment_id")
            .HasColumnType("uuid");
        builder.Property(x => x.SegmentType)
            .HasColumnName("segment_type")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.SegmentKind)
            .HasColumnName("segment_kind")
            .HasMaxLength(32)
            .HasDefaultValue("unknown")
            .IsRequired();
        builder.Property(x => x.Ordinal)
            .HasColumnName("ordinal")
            .IsRequired();
        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(1000);
        builder.Property(x => x.Text)
            .HasColumnName("text")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(x => x.Locator)
            .HasColumnName("locator")
            .HasMaxLength(500);

        builder.HasOne<KnowledgeResourceEntity>()
            .WithOne()
            .HasForeignKey<KnowledgeDocumentSegmentEntity>(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeArtifactEntity>()
            .WithMany()
            .HasForeignKey(x => x.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<KnowledgeDocumentSegmentEntity>()
            .WithMany()
            .HasForeignKey(x => x.ParentSegmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ArtifactId, x.ParentSegmentId, x.Ordinal })
            .HasDatabaseName("ix_knowledge_segments_structure");
        builder.HasIndex(x => new { x.ArtifactId, x.Locator })
            .HasDatabaseName("ix_knowledge_segments_locator");
    }

    private static void ConfigureRetrievalChunk(
        EntityTypeBuilder<KnowledgeRetrievalChunkEntity> builder)
    {
        builder.ToTable(
            "knowledge_retrieval_chunks",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_chunk_ordinal",
                    "ordinal >= 0");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.ArtifactId)
            .HasColumnName("artifact_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.Ordinal)
            .HasColumnName("ordinal")
            .IsRequired();
        builder.Property(x => x.Text)
            .HasColumnName("text")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(x => x.ChunkingStrategy)
            .HasColumnName("chunking_strategy")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.ChunkingVersion)
            .HasColumnName("chunking_version")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<KnowledgeArtifactEntity>()
            .WithMany()
            .HasForeignKey(x => x.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(x => new
            {
                x.ArtifactId,
                x.ChunkingStrategy,
                x.ChunkingVersion,
                x.Ordinal
            })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_retrieval_chunks_projection");
    }

    private static void ConfigureChunkSegment(
        EntityTypeBuilder<KnowledgeRetrievalChunkSegmentEntity> builder)
    {
        builder.ToTable(
            "knowledge_chunk_segments",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_chunk_segment_sequence",
                    "sequence >= 0");
                table.HasCheckConstraint(
                    "ck_knowledge_chunk_segment_offsets",
                    "start_offset >= 0 AND end_offset > start_offset");
            });

        builder.HasKey(x => new { x.ChunkId, x.SegmentId });

        builder.Property(x => x.ChunkId)
            .HasColumnName("chunk_id")
            .HasColumnType("uuid");
        builder.Property(x => x.SegmentId)
            .HasColumnName("segment_id")
            .HasColumnType("uuid");
        builder.Property(x => x.Sequence)
            .HasColumnName("sequence")
            .IsRequired();
        builder.Property(x => x.StartOffset)
            .HasColumnName("start_offset")
            .IsRequired();
        builder.Property(x => x.EndOffset)
            .HasColumnName("end_offset")
            .IsRequired();

        builder.HasOne<KnowledgeRetrievalChunkEntity>()
            .WithMany()
            .HasForeignKey(x => x.ChunkId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeDocumentSegmentEntity>()
            .WithMany()
            .HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => new { x.ChunkId, x.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_chunk_segments_sequence");
    }

    private static void ConfigureChunkEmbedding(
        EntityTypeBuilder<KnowledgeChunkEmbeddingEntity> builder)
    {
        builder.ToTable(
            "knowledge_chunk_embeddings",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_chunk_embedding_dimensions",
                    "dimensions BETWEEN 1 AND 16000 AND vector_dims(embedding) = dimensions");
                table.HasCheckConstraint(
                    "ck_knowledge_chunk_embedding_digest",
                    "model_digest ~ '^[0-9a-f]{64}$'");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.ChunkId)
            .HasColumnName("chunk_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.EmbeddingProfile)
            .HasColumnName("embedding_profile")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.Model)
            .HasColumnName("model")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(x => x.ModelDigest)
            .HasColumnName("model_digest")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(x => x.Dimensions)
            .HasColumnName("dimensions")
            .IsRequired();
        builder.Property(x => x.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector")
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<KnowledgeRetrievalChunkEntity>()
            .WithMany()
            .HasForeignKey(x => x.ChunkId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(x => new { x.ChunkId, x.EmbeddingProfile })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_chunk_embeddings_profile");
        builder.HasIndex(x => new { x.EmbeddingProfile, x.ModelDigest })
            .HasDatabaseName("ix_knowledge_chunk_embeddings_model");
    }

    private static void ConfigureMetadataAssertion(
        EntityTypeBuilder<KnowledgeMetadataAssertionEntity> builder)
    {
        builder.ToTable(
            "knowledge_metadata_assertions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_metadata_origin",
                    "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                table.HasCheckConstraint(
                    "ck_knowledge_metadata_review",
                    "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                table.HasCheckConstraint(
                    "ck_knowledge_metadata_review_time",
                    "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                table.HasCheckConstraint(
                    "ck_knowledge_metadata_confidence",
                    "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                table.HasCheckConstraint(
                    "ck_knowledge_metadata_supersedes",
                    "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.Property)
            .HasColumnName("property")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.Value)
            .HasColumnName("value")
            .HasColumnType("text")
            .IsRequired();
        ConfigureAssertionCommon(
            builder.Property(x => x.AssertionOrigin),
            builder.Property(x => x.AssertedBy),
            builder.Property(x => x.AssertedAt),
            builder.Property(x => x.ReviewStatus),
            builder.Property(x => x.ReviewedBy),
            builder.Property(x => x.ReviewedAt),
            builder.Property(x => x.Justification),
            builder.Property(x => x.SupportingSegmentId));
        builder.Property(x => x.Confidence)
            .HasColumnName("confidence");
        builder.Property(x => x.SupersedesAssertionId)
            .HasColumnName("supersedes_assertion_id")
            .HasColumnType("uuid");

        builder.HasOne<KnowledgeResourceEntity>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeDocumentSegmentEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupportingSegmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeMetadataAssertionEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupersedesAssertionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ResourceId, x.Property, x.ReviewStatus })
            .HasDatabaseName("ix_knowledge_metadata_resource_property");
    }

    private static void ConfigureSourceKind(EntityTypeBuilder<KnowledgeSourceKindEntity> builder)
    {
        builder.ToTable("knowledge_source_kinds");
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        ConfigureControlledTerm(
            builder.Property(x => x.Code),
            builder.Property(x => x.Label),
            builder.Property(x => x.Description));

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_source_kinds_code");
    }

    private static void ConfigureSourceKindAssertion(
        EntityTypeBuilder<KnowledgeSourceKindAssertionEntity> builder)
    {
        builder.ToTable(
            "knowledge_source_kind_assertions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_source_kind_origin",
                    "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                table.HasCheckConstraint(
                    "ck_knowledge_source_kind_review",
                    "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                table.HasCheckConstraint(
                    "ck_knowledge_source_kind_review_time",
                    "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                table.HasCheckConstraint(
                    "ck_knowledge_source_kind_supersedes",
                    "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.SourceKindId)
            .HasColumnName("source_kind_id")
            .HasColumnType("uuid")
            .IsRequired();
        ConfigureAssertionCommon(
            builder.Property(x => x.AssertionOrigin),
            builder.Property(x => x.AssertedBy),
            builder.Property(x => x.AssertedAt),
            builder.Property(x => x.ReviewStatus),
            builder.Property(x => x.ReviewedBy),
            builder.Property(x => x.ReviewedAt),
            builder.Property(x => x.Justification),
            builder.Property(x => x.SupportingSegmentId));
        builder.Property(x => x.SupersedesAssertionId)
            .HasColumnName("supersedes_assertion_id")
            .HasColumnType("uuid");

        builder.HasOne<KnowledgeResourceEntity>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeSourceKindEntity>()
            .WithMany()
            .HasForeignKey(x => x.SourceKindId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<KnowledgeDocumentSegmentEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupportingSegmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeSourceKindAssertionEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupersedesAssertionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ResourceId, x.SourceKindId, x.ReviewStatus })
            .HasDatabaseName("ix_knowledge_source_kind_assertions");
    }

    private static void ConfigurePerspective(EntityTypeBuilder<KnowledgePerspectiveEntity> builder)
    {
        builder.ToTable(
            "knowledge_perspectives",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_perspective_parent",
                    "parent_perspective_id IS NULL OR parent_perspective_id <> id");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.Label)
            .HasColumnName("label")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(x => x.ParentPerspectiveId)
            .HasColumnName("parent_perspective_id")
            .HasColumnType("uuid");
        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");
        builder.Property(x => x.HistoricalPeriod)
            .HasColumnName("historical_period")
            .HasMaxLength(255);

        builder.HasOne<KnowledgePerspectiveEntity>()
            .WithMany()
            .HasForeignKey(x => x.ParentPerspectiveId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_perspectives_code");
    }

    private static void ConfigurePerspectiveAssertion(
        EntityTypeBuilder<KnowledgePerspectiveAssertionEntity> builder)
    {
        builder.ToTable(
            "knowledge_perspective_assertions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_perspective_origin",
                    "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                table.HasCheckConstraint(
                    "ck_knowledge_perspective_review",
                    "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                table.HasCheckConstraint(
                    "ck_knowledge_perspective_review_time",
                    "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                table.HasCheckConstraint(
                    "ck_knowledge_perspective_type",
                    "perspective_type IN ('declared', 'analytical')");
                table.HasCheckConstraint(
                    "ck_knowledge_perspective_supersedes",
                    "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.PerspectiveId)
            .HasColumnName("perspective_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.PerspectiveType)
            .HasColumnName("perspective_type")
            .HasMaxLength(32)
            .IsRequired();
        ConfigureAssertionCommon(
            builder.Property(x => x.AssertionOrigin),
            builder.Property(x => x.AssertedBy),
            builder.Property(x => x.AssertedAt),
            builder.Property(x => x.ReviewStatus),
            builder.Property(x => x.ReviewedBy),
            builder.Property(x => x.ReviewedAt),
            builder.Property(x => x.Justification),
            builder.Property(x => x.SupportingSegmentId));
        builder.Property(x => x.SupersedesAssertionId)
            .HasColumnName("supersedes_assertion_id")
            .HasColumnType("uuid");

        builder.HasOne<KnowledgeResourceEntity>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgePerspectiveEntity>()
            .WithMany()
            .HasForeignKey(x => x.PerspectiveId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<KnowledgeDocumentSegmentEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupportingSegmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgePerspectiveAssertionEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupersedesAssertionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ResourceId, x.PerspectiveId, x.ReviewStatus })
            .HasDatabaseName("ix_knowledge_perspective_assertions");
    }

    private static void ConfigureMethodologicalFramework(
        EntityTypeBuilder<KnowledgeMethodologicalFrameworkEntity> builder)
    {
        builder.ToTable("knowledge_methodological_frameworks");
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        ConfigureControlledTerm(
            builder.Property(x => x.Code),
            builder.Property(x => x.Label),
            builder.Property(x => x.Description));
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_methodological_frameworks_code");
    }

    private static void ConfigureMethodologicalFrameworkAssertion(
        EntityTypeBuilder<KnowledgeMethodologicalFrameworkAssertionEntity> builder)
    {
        builder.ToTable(
            "knowledge_methodological_framework_assertions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_methodological_framework_origin",
                    "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                table.HasCheckConstraint(
                    "ck_knowledge_methodological_framework_review",
                    "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                table.HasCheckConstraint(
                    "ck_knowledge_methodological_framework_review_time",
                    "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                table.HasCheckConstraint(
                    "ck_knowledge_methodological_framework_type",
                    "classification_type IN ('declared', 'analytical')");
                table.HasCheckConstraint(
                    "ck_knowledge_methodological_framework_supersedes",
                    "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
            });
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.MethodologicalFrameworkId)
            .HasColumnName("methodological_framework_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.ClassificationType)
            .HasColumnName("classification_type")
            .HasMaxLength(32)
            .IsRequired();
        ConfigureAssertionCommon(
            builder.Property(x => x.AssertionOrigin),
            builder.Property(x => x.AssertedBy),
            builder.Property(x => x.AssertedAt),
            builder.Property(x => x.ReviewStatus),
            builder.Property(x => x.ReviewedBy),
            builder.Property(x => x.ReviewedAt),
            builder.Property(x => x.Justification),
            builder.Property(x => x.SupportingSegmentId));
        builder.Property(x => x.SupersedesAssertionId)
            .HasColumnName("supersedes_assertion_id")
            .HasColumnType("uuid");
        builder.HasOne<KnowledgeResourceEntity>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .HasConstraintName("fk_method_framework_assertion_resource")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeMethodologicalFrameworkEntity>()
            .WithMany()
            .HasForeignKey(x => x.MethodologicalFrameworkId)
            .HasConstraintName("fk_method_framework_assertion_framework")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<KnowledgeDocumentSegmentEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupportingSegmentId)
            .HasConstraintName("fk_method_framework_assertion_support_segment")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeMethodologicalFrameworkAssertionEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupersedesAssertionId)
            .HasConstraintName("fk_method_framework_assertion_supersedes")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new
            {
                x.ResourceId,
                x.MethodologicalFrameworkId,
                x.ReviewStatus
            })
            .HasDatabaseName("ix_knowledge_methodological_framework_assertions");
    }

    private static void ConfigureEpistemicFramework(
        EntityTypeBuilder<KnowledgeEpistemicFrameworkEntity> builder)
    {
        builder.ToTable("knowledge_epistemic_frameworks");
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        ConfigureControlledTerm(
            builder.Property(x => x.Code),
            builder.Property(x => x.Label),
            builder.Property(x => x.Description));
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_epistemic_frameworks_code");
    }

    private static void ConfigureEpistemicFrameworkAssertion(
        EntityTypeBuilder<KnowledgeEpistemicFrameworkAssertionEntity> builder)
    {
        builder.ToTable(
            "knowledge_epistemic_framework_assertions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_epistemic_framework_origin",
                    "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                table.HasCheckConstraint(
                    "ck_knowledge_epistemic_framework_review",
                    "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                table.HasCheckConstraint(
                    "ck_knowledge_epistemic_framework_review_time",
                    "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                table.HasCheckConstraint(
                    "ck_knowledge_epistemic_framework_type",
                    "classification_type IN ('declared', 'analytical')");
                table.HasCheckConstraint(
                    "ck_knowledge_epistemic_framework_supersedes",
                    "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
            });
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.EpistemicFrameworkId)
            .HasColumnName("epistemic_framework_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.ClassificationType)
            .HasColumnName("classification_type")
            .HasMaxLength(32)
            .IsRequired();
        ConfigureAssertionCommon(
            builder.Property(x => x.AssertionOrigin),
            builder.Property(x => x.AssertedBy),
            builder.Property(x => x.AssertedAt),
            builder.Property(x => x.ReviewStatus),
            builder.Property(x => x.ReviewedBy),
            builder.Property(x => x.ReviewedAt),
            builder.Property(x => x.Justification),
            builder.Property(x => x.SupportingSegmentId));
        builder.Property(x => x.SupersedesAssertionId)
            .HasColumnName("supersedes_assertion_id")
            .HasColumnType("uuid");
        builder.HasOne<KnowledgeResourceEntity>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .HasConstraintName("fk_epistemic_framework_assertion_resource")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeEpistemicFrameworkEntity>()
            .WithMany()
            .HasForeignKey(x => x.EpistemicFrameworkId)
            .HasConstraintName("fk_epistemic_framework_assertion_framework")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<KnowledgeDocumentSegmentEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupportingSegmentId)
            .HasConstraintName("fk_epistemic_framework_assertion_support_segment")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeEpistemicFrameworkAssertionEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupersedesAssertionId)
            .HasConstraintName("fk_epistemic_framework_assertion_supersedes")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new
            {
                x.ResourceId,
                x.EpistemicFrameworkId,
                x.ReviewStatus
            })
            .HasDatabaseName("ix_knowledge_epistemic_framework_assertions");
    }

    private static void ConfigureEvidenceRole(EntityTypeBuilder<KnowledgeEvidenceRoleEntity> builder)
    {
        builder.ToTable("knowledge_evidence_roles");
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        ConfigureControlledTerm(
            builder.Property(x => x.Code),
            builder.Property(x => x.Label),
            builder.Property(x => x.Description));

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_evidence_roles_code");
    }

    private static void ConfigureEvidenceRoleAssertion(
        EntityTypeBuilder<KnowledgeEvidenceRoleAssertionEntity> builder)
    {
        builder.ToTable(
            "knowledge_evidence_role_assertions",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_knowledge_evidence_role_origin",
                    "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                table.HasCheckConstraint(
                    "ck_knowledge_evidence_role_review",
                    "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                table.HasCheckConstraint(
                    "ck_knowledge_evidence_role_review_time",
                    "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                table.HasCheckConstraint(
                    "ck_knowledge_evidence_role_supersedes",
                    "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
            });

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));
        builder.Property(x => x.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.EvidenceRoleId)
            .HasColumnName("evidence_role_id")
            .HasColumnType("uuid")
            .IsRequired();
        ConfigureAssertionCommon(
            builder.Property(x => x.AssertionOrigin),
            builder.Property(x => x.AssertedBy),
            builder.Property(x => x.AssertedAt),
            builder.Property(x => x.ReviewStatus),
            builder.Property(x => x.ReviewedBy),
            builder.Property(x => x.ReviewedAt),
            builder.Property(x => x.Justification),
            builder.Property(x => x.SupportingSegmentId));
        builder.Property(x => x.SupersedesAssertionId)
            .HasColumnName("supersedes_assertion_id")
            .HasColumnType("uuid");

        builder.HasOne<KnowledgeResourceEntity>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        builder.HasOne<KnowledgeEvidenceRoleEntity>()
            .WithMany()
            .HasForeignKey(x => x.EvidenceRoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
        builder.HasOne<KnowledgeDocumentSegmentEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupportingSegmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeEvidenceRoleAssertionEntity>()
            .WithMany()
            .HasForeignKey(x => x.SupersedesAssertionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ResourceId, x.EvidenceRoleId, x.ReviewStatus })
            .HasDatabaseName("ix_knowledge_evidence_role_assertions");
    }

    private static void ConfigureUuidId(PropertyBuilder<Guid> property)
    {
        property
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
    }

    private static void ConfigureIdentifier(
        PropertyBuilder<string> scheme,
        PropertyBuilder<string> value,
        PropertyBuilder<string?> uri)
    {
        scheme
            .HasColumnName("scheme")
            .HasMaxLength(64)
            .IsRequired();
        value
            .HasColumnName("value")
            .HasMaxLength(500)
            .IsRequired();
        uri
            .HasColumnName("uri")
            .HasMaxLength(2048);
    }

    private static void ConfigureControlledTerm(
        PropertyBuilder<string> code,
        PropertyBuilder<string> label,
        PropertyBuilder<string?> description)
    {
        code
            .HasColumnName("code")
            .HasMaxLength(128)
            .IsRequired();
        label
            .HasColumnName("label")
            .HasMaxLength(255)
            .IsRequired();
        description
            .HasColumnName("description")
            .HasColumnType("text");
    }

    private static void ConfigureAssertionCommon(
        PropertyBuilder<string> origin,
        PropertyBuilder<string> assertedBy,
        PropertyBuilder<DateTimeOffset> assertedAt,
        PropertyBuilder<string> reviewStatus,
        PropertyBuilder<string?> reviewedBy,
        PropertyBuilder<DateTimeOffset?> reviewedAt,
        PropertyBuilder<string?> justification,
        PropertyBuilder<Guid?> supportingSegmentId)
    {
        origin
            .HasColumnName("assertion_origin")
            .HasMaxLength(32)
            .IsRequired();
        assertedBy
            .HasColumnName("asserted_by")
            .HasMaxLength(255)
            .IsRequired();
        assertedAt
            .HasColumnName("asserted_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        reviewStatus
            .HasColumnName("review_status")
            .HasMaxLength(32)
            .IsRequired();
        reviewedBy
            .HasColumnName("reviewed_by")
            .HasMaxLength(255);
        reviewedAt
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");
        justification
            .HasColumnName("justification")
            .HasColumnType("text");
        supportingSegmentId
            .HasColumnName("supporting_segment_id")
            .HasColumnType("uuid");
    }

    private static void ConfigureGenreFormAuthoritySnapshot(
        EntityTypeBuilder<GenreFormAuthoritySnapshotEntity> builder)
    {
        builder.ToTable("genre_form_authority_snapshots");
        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));

        builder.Property(x => x.Authority)
            .HasColumnName("authority")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.SourceUri)
            .HasColumnName("source_uri")
            .HasMaxLength(1024)
            .IsRequired();
        builder.Property(x => x.ContentSha256)
            .HasColumnName("content_sha256")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.RetrievedAt)
            .HasColumnName("retrieved_at")
            .IsRequired();
        builder.Property(x => x.ImporterVersion)
            .HasColumnName("importer_version")
            .HasMaxLength(64);
        builder.Property(x => x.TermCount)
            .HasColumnName("term_count")
            .IsRequired();

        builder.HasIndex(x => new { x.Authority, x.ContentSha256 })
            .IsUnique()
            .HasDatabaseName("ux_genre_form_authority_snapshots_content");
    }

    private static void ConfigureGenreFormAuthorityTerm(
        EntityTypeBuilder<GenreFormAuthorityTermEntity> builder)
    {
        builder.ToTable(
            "genre_form_authority_terms",
            table => table.HasCheckConstraint(
                "ck_genre_form_authority_term_status",
                "authority_status IN ('active', 'deprecated')"));

        builder.HasKey(x => x.Id);
        ConfigureUuidId(builder.Property(x => x.Id));

        builder.Property(x => x.Authority)
            .HasColumnName("authority")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.AuthorityIdentifier)
            .HasColumnName("authority_identifier")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(x => x.AuthorityUri)
            .HasColumnName("authority_uri")
            .HasMaxLength(1024)
            .IsRequired();
        builder.Property(x => x.PreferredLabel)
            .HasColumnName("preferred_label")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(x => x.LanguageCode)
            .HasColumnName("language_code")
            .HasMaxLength(35);
        builder.Property(x => x.AuthorityStatus)
            .HasColumnName("authority_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.SnapshotId)
            .HasColumnName("snapshot_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasOne<GenreFormAuthoritySnapshotEntity>()
            .WithMany()
            .HasForeignKey(x => x.SnapshotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AuthorityUri)
            .IsUnique()
            .HasDatabaseName("ux_genre_form_authority_terms_uri");
        builder.HasIndex(x => new { x.Authority, x.AuthorityStatus })
            .HasDatabaseName("ix_genre_form_authority_terms_status");
    }

    private static void ConfigureGenreFormAuthorityVariant(
        EntityTypeBuilder<GenreFormAuthorityVariantEntity> builder)
    {
        builder.ToTable("genre_form_authority_variants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.TermId)
            .HasColumnName("term_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.Label)
            .HasColumnName("label")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(x => x.LanguageCode)
            .HasColumnName("language_code")
            .HasMaxLength(35);

        builder.HasOne<GenreFormAuthorityTermEntity>()
            .WithMany()
            .HasForeignKey(x => x.TermId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TermId, x.Label })
            .IsUnique()
            .HasDatabaseName("ux_genre_form_authority_variants");
    }

    private static void ConfigureGenreFormAuthorityNote(
        EntityTypeBuilder<GenreFormAuthorityNoteEntity> builder)
    {
        builder.ToTable(
            "genre_form_authority_notes",
            table => table.HasCheckConstraint(
                "ck_genre_form_authority_note_type",
                "note_type IN ('general', 'history', 'example')"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.TermId)
            .HasColumnName("term_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.NoteType)
            .HasColumnName("note_type")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.Text)
            .HasColumnName("text")
            .IsRequired();

        builder.HasOne<GenreFormAuthorityTermEntity>()
            .WithMany()
            .HasForeignKey(x => x.TermId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TermId, x.NoteType })
            .HasDatabaseName("ix_genre_form_authority_notes");
    }

    private static void ConfigureGenreFormBroaderRelation(
        EntityTypeBuilder<GenreFormBroaderRelationEntity> builder)
    {
        builder.ToTable(
            "genre_form_broader_relations",
            table => table.HasCheckConstraint(
                "ck_genre_form_broader_distinct",
                "narrower_term_id <> broader_term_id"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.NarrowerTermId)
            .HasColumnName("narrower_term_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.BroaderTermId)
            .HasColumnName("broader_term_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasOne<GenreFormAuthorityTermEntity>()
            .WithMany()
            .HasForeignKey(x => x.NarrowerTermId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GenreFormAuthorityTermEntity>()
            .WithMany()
            .HasForeignKey(x => x.BroaderTermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.NarrowerTermId, x.BroaderTermId })
            .IsUnique()
            .HasDatabaseName("ux_genre_form_broader_relations");
        builder.HasIndex(x => x.BroaderTermId)
            .HasDatabaseName("ix_genre_form_broader_relations_broader");
    }

    private static void ConfigureGenreFormRelatedRelation(
        EntityTypeBuilder<GenreFormRelatedRelationEntity> builder)
    {
        builder.ToTable(
            "genre_form_related_relations",
            table => table.HasCheckConstraint(
                "ck_genre_form_related_canonical",
                "term_id_a < term_id_b"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.TermIdA)
            .HasColumnName("term_id_a")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.TermIdB)
            .HasColumnName("term_id_b")
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasOne<GenreFormAuthorityTermEntity>()
            .WithMany()
            .HasForeignKey(x => x.TermIdA)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GenreFormAuthorityTermEntity>()
            .WithMany()
            .HasForeignKey(x => x.TermIdB)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TermIdA, x.TermIdB })
            .IsUnique()
            .HasDatabaseName("ux_genre_form_related_relations");
    }

    private static void ConfigureGenreFormProfileEntry(
        EntityTypeBuilder<GenreFormProfileEntryEntity> builder)
    {
        builder.ToTable(
            "genre_form_profile_entries",
            table => table.HasCheckConstraint(
                "ck_genre_form_profile_usage",
                "usage_status IN ('excluded', 'structural_only', 'selectable')"));

        builder.HasKey(x => x.TermId);

        builder.Property(x => x.TermId)
            .HasColumnName("term_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property(x => x.UsageStatus)
            .HasColumnName("usage_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.DisplayOrder)
            .HasColumnName("display_order");
        builder.Property(x => x.ProfileVersion)
            .HasColumnName("profile_version")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne<GenreFormAuthorityTermEntity>()
            .WithMany()
            .HasForeignKey(x => x.TermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UsageStatus)
            .HasDatabaseName("ix_genre_form_profile_entries_usage");
    }

    private static void ConfigureKnowledgeWorkGenreForm(
        EntityTypeBuilder<KnowledgeWorkGenreFormEntity> builder)
    {
        builder.ToTable("knowledge_work_genre_forms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.WorkId)
            .HasColumnName("work_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.Property(x => x.TermId)
            .HasColumnName("term_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasOne<KnowledgeWorkEntity>()
            .WithMany()
            .HasForeignKey(x => x.WorkId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GenreFormAuthorityTermEntity>()
            .WithMany()
            .HasForeignKey(x => x.TermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.WorkId, x.TermId })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_work_genre_forms");
    }
}
