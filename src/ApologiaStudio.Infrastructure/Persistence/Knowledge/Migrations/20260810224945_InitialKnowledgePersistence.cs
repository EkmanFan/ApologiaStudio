using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class InitialKnowledgePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "knowledge_evidence_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_evidence_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_perspectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    parent_perspective_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    historical_period = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_perspectives", x => x.id);
                    table.CheckConstraint("ck_knowledge_perspective_parent", "parent_perspective_id IS NULL OR parent_perspective_id <> id");
                    table.ForeignKey(
                        name: "FK_knowledge_perspectives_knowledge_perspectives_parent_perspe~",
                        column: x => x.parent_perspective_id,
                        principalTable: "knowledge_perspectives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    editorial_review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_resources", x => x.id);
                    table.CheckConstraint("ck_knowledge_resources_review", "editorial_review_status IN ('pending', 'in_review', 'approved', 'rejected')");
                });

            migrationBuilder.CreateTable(
                name: "knowledge_source_kinds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_source_kinds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_contributors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    preferred_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sort_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_contributors", x => x.id);
                    table.CheckConstraint("ck_knowledge_contributor_type", "contributor_type IN ('person', 'collective_body')");
                    table.ForeignKey(
                        name: "FK_knowledge_contributors_knowledge_resources_id",
                        column: x => x.id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_works",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_works", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_works_knowledge_resources_id",
                        column: x => x.id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_contributor_identifiers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_contributor_identifiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_contributor_identifiers_knowledge_contributors_co~",
                        column: x => x.contributor_id,
                        principalTable: "knowledge_contributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_expressions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_expressions", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_expressions_knowledge_resources_id",
                        column: x => x.id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_expressions_knowledge_works_work_id",
                        column: x => x.work_id,
                        principalTable: "knowledge_works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_expression_relations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_expression_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_expression_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_expression_relations", x => x.id);
                    table.CheckConstraint("ck_knowledge_expr_rel_distinct", "from_expression_id <> to_expression_id");
                    table.CheckConstraint("ck_knowledge_expr_rel_type", "relation_type IN ('translation_of', 'revision_of', 'adaptation_of', 'derived_from')");
                    table.ForeignKey(
                        name: "FK_knowledge_expression_relations_knowledge_expressions_from_e~",
                        column: x => x.from_expression_id,
                        principalTable: "knowledge_expressions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_expression_relations_knowledge_expressions_to_exp~",
                        column: x => x.to_expression_id,
                        principalTable: "knowledge_expressions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_manifestations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expression_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edition_statement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    publication_year = table.Column<int>(type: "integer", nullable: true),
                    publication_place = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    citation_label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_manifestations", x => x.id);
                    table.CheckConstraint("ck_knowledge_manifestation_year", "publication_year IS NULL OR publication_year BETWEEN 1 AND 9999");
                    table.ForeignKey(
                        name: "FK_knowledge_manifestations_knowledge_expressions_expression_id",
                        column: x => x.expression_id,
                        principalTable: "knowledge_expressions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_manifestations_knowledge_resources_id",
                        column: x => x.id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_artifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifestation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    derived_from_artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    artifact_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    media_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false),
                    origin_uri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    acquired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lifecycle_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_artifacts", x => x.id);
                    table.CheckConstraint("ck_knowledge_artifact_derivation", "derived_from_artifact_id IS NULL OR derived_from_artifact_id <> id");
                    table.CheckConstraint("ck_knowledge_artifact_length", "byte_length >= 0");
                    table.CheckConstraint("ck_knowledge_artifact_lifecycle", "lifecycle_status IN ('active', 'superseded', 'retired', 'corrupted', 'deleted')");
                    table.CheckConstraint("ck_knowledge_artifact_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_knowledge_artifact_type", "artifact_type IN ('raw', 'ocr', 'parsed', 'normalized')");
                    table.ForeignKey(
                        name: "FK_knowledge_artifacts_knowledge_artifacts_derived_from_artifa~",
                        column: x => x.derived_from_artifact_id,
                        principalTable: "knowledge_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_artifacts_knowledge_manifestations_manifestation_~",
                        column: x => x.manifestation_id,
                        principalTable: "knowledge_manifestations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_artifacts_knowledge_resources_id",
                        column: x => x.id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_contributions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expression_id = table.Column<Guid>(type: "uuid", nullable: true),
                    manifestation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attribution_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_contributions", x => x.id);
                    table.CheckConstraint("ck_knowledge_contribution_attribution", "attribution_status IN ('explicit', 'established', 'traditional', 'probable', 'possible', 'disputed')");
                    table.CheckConstraint("ck_knowledge_contribution_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_knowledge_contribution_role", "role IN ('author', 'corporate_author', 'compiler', 'issuing_body', 'translator', 'reviser', 'textual_editor', 'transcriber', 'commentator', 'publisher', 'series_editor', 'distributor', 'producer')");
                    table.CheckConstraint("ck_knowledge_contribution_target", "(CASE WHEN work_id IS NULL THEN 0 ELSE 1 END + CASE WHEN expression_id IS NULL THEN 0 ELSE 1 END + CASE WHEN manifestation_id IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_knowledge_contributors_contributor_~",
                        column: x => x.contributor_id,
                        principalTable: "knowledge_contributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_knowledge_expressions_expression_id",
                        column: x => x.expression_id,
                        principalTable: "knowledge_expressions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_knowledge_manifestations_manifestat~",
                        column: x => x.manifestation_id,
                        principalTable: "knowledge_manifestations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_contributions_knowledge_works_work_id",
                        column: x => x.work_id,
                        principalTable: "knowledge_works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_manifestation_identifiers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    manifestation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    uri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_manifestation_identifiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_manifestation_identifiers_knowledge_manifestation~",
                        column: x => x.manifestation_id,
                        principalTable: "knowledge_manifestations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_document_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_segment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    segment_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    locator = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_document_segments", x => x.id);
                    table.CheckConstraint("ck_knowledge_segment_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_knowledge_segment_parent", "parent_segment_id IS NULL OR parent_segment_id <> id");
                    table.ForeignKey(
                        name: "FK_knowledge_document_segments_knowledge_artifacts_artifact_id",
                        column: x => x.artifact_id,
                        principalTable: "knowledge_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_document_segments_knowledge_document_segments_par~",
                        column: x => x.parent_segment_id,
                        principalTable: "knowledge_document_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_document_segments_knowledge_resources_id",
                        column: x => x.id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_processing_activities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    input_artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    output_artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tool_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tool_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    configuration_json = table.Column<string>(type: "jsonb", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_processing_activities", x => x.id);
                    table.CheckConstraint("ck_knowledge_processing_artifacts", "input_artifact_id IS NULL OR input_artifact_id <> output_artifact_id");
                    table.CheckConstraint("ck_knowledge_processing_status", "status IN ('pending', 'completed', 'failed')");
                    table.CheckConstraint("ck_knowledge_processing_time", "completed_at IS NULL OR completed_at >= started_at");
                    table.CheckConstraint("ck_knowledge_processing_type", "activity_type IN ('download', 'ocr', 'parse', 'normalize', 'correct')");
                    table.ForeignKey(
                        name: "FK_knowledge_processing_activities_knowledge_artifacts_input_a~",
                        column: x => x.input_artifact_id,
                        principalTable: "knowledge_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_processing_activities_knowledge_artifacts_output_~",
                        column: x => x.output_artifact_id,
                        principalTable: "knowledge_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_retrieval_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    chunking_strategy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    chunking_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_retrieval_chunks", x => x.id);
                    table.CheckConstraint("ck_knowledge_chunk_ordinal", "ordinal >= 0");
                    table.ForeignKey(
                        name: "FK_knowledge_retrieval_chunks_knowledge_artifacts_artifact_id",
                        column: x => x.artifact_id,
                        principalTable: "knowledge_artifacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_evidence_role_assertions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assertion_origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    asserted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    asserted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    supporting_segment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_assertion_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_evidence_role_assertions", x => x.id);
                    table.CheckConstraint("ck_knowledge_evidence_role_origin", "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                    table.CheckConstraint("ck_knowledge_evidence_role_review", "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                    table.CheckConstraint("ck_knowledge_evidence_role_review_time", "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                    table.CheckConstraint("ck_knowledge_evidence_role_supersedes", "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
                    table.ForeignKey(
                        name: "FK_knowledge_evidence_role_assertions_knowledge_document_segme~",
                        column: x => x.supporting_segment_id,
                        principalTable: "knowledge_document_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_evidence_role_assertions_knowledge_evidence_role_~",
                        column: x => x.supersedes_assertion_id,
                        principalTable: "knowledge_evidence_role_assertions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_evidence_role_assertions_knowledge_evidence_roles~",
                        column: x => x.evidence_role_id,
                        principalTable: "knowledge_evidence_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_evidence_role_assertions_knowledge_resources_reso~",
                        column: x => x.resource_id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_metadata_assertions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    assertion_origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    asserted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    asserted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    supporting_segment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_assertion_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_metadata_assertions", x => x.id);
                    table.CheckConstraint("ck_knowledge_metadata_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                    table.CheckConstraint("ck_knowledge_metadata_origin", "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                    table.CheckConstraint("ck_knowledge_metadata_review", "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                    table.CheckConstraint("ck_knowledge_metadata_review_time", "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                    table.CheckConstraint("ck_knowledge_metadata_supersedes", "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
                    table.ForeignKey(
                        name: "FK_knowledge_metadata_assertions_knowledge_document_segments_s~",
                        column: x => x.supporting_segment_id,
                        principalTable: "knowledge_document_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_metadata_assertions_knowledge_metadata_assertions~",
                        column: x => x.supersedes_assertion_id,
                        principalTable: "knowledge_metadata_assertions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_metadata_assertions_knowledge_resources_resource_~",
                        column: x => x.resource_id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_perspective_assertions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perspective_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perspective_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assertion_origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    asserted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    asserted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    supporting_segment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_assertion_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_perspective_assertions", x => x.id);
                    table.CheckConstraint("ck_knowledge_perspective_origin", "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                    table.CheckConstraint("ck_knowledge_perspective_review", "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                    table.CheckConstraint("ck_knowledge_perspective_review_time", "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                    table.CheckConstraint("ck_knowledge_perspective_supersedes", "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
                    table.CheckConstraint("ck_knowledge_perspective_type", "perspective_type IN ('declared', 'analytical')");
                    table.ForeignKey(
                        name: "FK_knowledge_perspective_assertions_knowledge_document_segment~",
                        column: x => x.supporting_segment_id,
                        principalTable: "knowledge_document_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_perspective_assertions_knowledge_perspective_asse~",
                        column: x => x.supersedes_assertion_id,
                        principalTable: "knowledge_perspective_assertions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_perspective_assertions_knowledge_perspectives_per~",
                        column: x => x.perspective_id,
                        principalTable: "knowledge_perspectives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_perspective_assertions_knowledge_resources_resour~",
                        column: x => x.resource_id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_source_kind_assertions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_kind_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assertion_origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    asserted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    asserted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    supporting_segment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_assertion_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_source_kind_assertions", x => x.id);
                    table.CheckConstraint("ck_knowledge_source_kind_origin", "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                    table.CheckConstraint("ck_knowledge_source_kind_review", "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                    table.CheckConstraint("ck_knowledge_source_kind_review_time", "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                    table.CheckConstraint("ck_knowledge_source_kind_supersedes", "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
                    table.ForeignKey(
                        name: "FK_knowledge_source_kind_assertions_knowledge_document_segment~",
                        column: x => x.supporting_segment_id,
                        principalTable: "knowledge_document_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_source_kind_assertions_knowledge_resources_resour~",
                        column: x => x.resource_id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_source_kind_assertions_knowledge_source_kind_asse~",
                        column: x => x.supersedes_assertion_id,
                        principalTable: "knowledge_source_kind_assertions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_source_kind_assertions_knowledge_source_kinds_sou~",
                        column: x => x.source_kind_id,
                        principalTable: "knowledge_source_kinds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunk_segments",
                columns: table => new
                {
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    segment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    start_offset = table.Column<int>(type: "integer", nullable: false),
                    end_offset = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunk_segments", x => new { x.chunk_id, x.segment_id });
                    table.CheckConstraint("ck_knowledge_chunk_segment_offsets", "start_offset >= 0 AND end_offset > start_offset");
                    table.CheckConstraint("ck_knowledge_chunk_segment_sequence", "sequence >= 0");
                    table.ForeignKey(
                        name: "FK_knowledge_chunk_segments_knowledge_document_segments_segmen~",
                        column: x => x.segment_id,
                        principalTable: "knowledge_document_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_chunk_segments_knowledge_retrieval_chunks_chunk_id",
                        column: x => x.chunk_id,
                        principalTable: "knowledge_retrieval_chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_artifacts_derived_from",
                table: "knowledge_artifacts",
                column: "derived_from_artifact_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_artifacts_manifestation_id",
                table: "knowledge_artifacts",
                column: "manifestation_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_artifacts_sha256",
                table: "knowledge_artifacts",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunk_segments_segment_id",
                table: "knowledge_chunk_segments",
                column: "segment_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_chunk_segments_sequence",
                table: "knowledge_chunk_segments",
                columns: new[] { "chunk_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_contributions_contributor",
                table: "knowledge_contributions",
                column: "contributor_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_expression_id",
                table: "knowledge_contributions",
                column: "expression_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_manifestation_id",
                table: "knowledge_contributions",
                column: "manifestation_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_contributions_work_id",
                table: "knowledge_contributions",
                column: "work_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_contributor_identifier",
                table: "knowledge_contributor_identifiers",
                columns: new[] { "contributor_id", "scheme", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_contributors_preferred_name",
                table: "knowledge_contributors",
                column: "preferred_name");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_document_segments_parent_segment_id",
                table: "knowledge_document_segments",
                column: "parent_segment_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_segments_locator",
                table: "knowledge_document_segments",
                columns: new[] { "artifact_id", "locator" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_segments_structure",
                table: "knowledge_document_segments",
                columns: new[] { "artifact_id", "parent_segment_id", "ordinal" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_evidence_role_assertions",
                table: "knowledge_evidence_role_assertions",
                columns: new[] { "resource_id", "evidence_role_id", "review_status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_evidence_role_assertions_evidence_role_id",
                table: "knowledge_evidence_role_assertions",
                column: "evidence_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_evidence_role_assertions_supersedes_assertion_id",
                table: "knowledge_evidence_role_assertions",
                column: "supersedes_assertion_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_evidence_role_assertions_supporting_segment_id",
                table: "knowledge_evidence_role_assertions",
                column: "supporting_segment_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_evidence_roles_code",
                table: "knowledge_evidence_roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_expression_relations_to_expression_id",
                table: "knowledge_expression_relations",
                column: "to_expression_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_expression_relations",
                table: "knowledge_expression_relations",
                columns: new[] { "from_expression_id", "to_expression_id", "relation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_expressions_work_language",
                table: "knowledge_expressions",
                columns: new[] { "work_id", "language_code" });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_manifestation_identifier",
                table: "knowledge_manifestation_identifiers",
                columns: new[] { "manifestation_id", "scheme", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_manifestations_expression",
                table: "knowledge_manifestations",
                column: "expression_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_metadata_assertions_supersedes_assertion_id",
                table: "knowledge_metadata_assertions",
                column: "supersedes_assertion_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_metadata_assertions_supporting_segment_id",
                table: "knowledge_metadata_assertions",
                column: "supporting_segment_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_metadata_resource_property",
                table: "knowledge_metadata_assertions",
                columns: new[] { "resource_id", "property", "review_status" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_perspective_assertions",
                table: "knowledge_perspective_assertions",
                columns: new[] { "resource_id", "perspective_id", "review_status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_perspective_assertions_perspective_id",
                table: "knowledge_perspective_assertions",
                column: "perspective_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_perspective_assertions_supersedes_assertion_id",
                table: "knowledge_perspective_assertions",
                column: "supersedes_assertion_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_perspective_assertions_supporting_segment_id",
                table: "knowledge_perspective_assertions",
                column: "supporting_segment_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_perspectives_parent_perspective_id",
                table: "knowledge_perspectives",
                column: "parent_perspective_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_perspectives_code",
                table: "knowledge_perspectives",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_processing_activities_input_artifact_id",
                table: "knowledge_processing_activities",
                column: "input_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_processing_output",
                table: "knowledge_processing_activities",
                column: "output_artifact_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_resources_review",
                table: "knowledge_resources",
                column: "editorial_review_status");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_retrieval_chunks_projection",
                table: "knowledge_retrieval_chunks",
                columns: new[] { "artifact_id", "chunking_strategy", "chunking_version", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_source_kind_assertions",
                table: "knowledge_source_kind_assertions",
                columns: new[] { "resource_id", "source_kind_id", "review_status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_source_kind_assertions_source_kind_id",
                table: "knowledge_source_kind_assertions",
                column: "source_kind_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_source_kind_assertions_supersedes_assertion_id",
                table: "knowledge_source_kind_assertions",
                column: "supersedes_assertion_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_source_kind_assertions_supporting_segment_id",
                table: "knowledge_source_kind_assertions",
                column: "supporting_segment_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_source_kinds_code",
                table: "knowledge_source_kinds",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_chunk_segments");

            migrationBuilder.DropTable(
                name: "knowledge_contributions");

            migrationBuilder.DropTable(
                name: "knowledge_contributor_identifiers");

            migrationBuilder.DropTable(
                name: "knowledge_evidence_role_assertions");

            migrationBuilder.DropTable(
                name: "knowledge_expression_relations");

            migrationBuilder.DropTable(
                name: "knowledge_manifestation_identifiers");

            migrationBuilder.DropTable(
                name: "knowledge_metadata_assertions");

            migrationBuilder.DropTable(
                name: "knowledge_perspective_assertions");

            migrationBuilder.DropTable(
                name: "knowledge_processing_activities");

            migrationBuilder.DropTable(
                name: "knowledge_source_kind_assertions");

            migrationBuilder.DropTable(
                name: "knowledge_retrieval_chunks");

            migrationBuilder.DropTable(
                name: "knowledge_contributors");

            migrationBuilder.DropTable(
                name: "knowledge_evidence_roles");

            migrationBuilder.DropTable(
                name: "knowledge_perspectives");

            migrationBuilder.DropTable(
                name: "knowledge_document_segments");

            migrationBuilder.DropTable(
                name: "knowledge_source_kinds");

            migrationBuilder.DropTable(
                name: "knowledge_artifacts");

            migrationBuilder.DropTable(
                name: "knowledge_manifestations");

            migrationBuilder.DropTable(
                name: "knowledge_expressions");

            migrationBuilder.DropTable(
                name: "knowledge_works");

            migrationBuilder.DropTable(
                name: "knowledge_resources");
        }
    }
}
