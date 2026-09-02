using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentManagerEditorialDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_manager_editorial_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifest_revision = table.Column<int>(type: "integer", nullable: false),
                    source_sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    title_origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    language_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    edition_statement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    publication_year = table.Column<int>(type: "integer", nullable: true),
                    publication_place = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_manager_editorial_drafts", x => x.id);
                    table.CheckConstraint("ck_document_manager_editorial_draft_publication_year", "publication_year IS NULL OR publication_year BETWEEN 1 AND 9999");
                    table.CheckConstraint("ck_document_manager_editorial_draft_revision", "manifest_revision > 0");
                    table.CheckConstraint("ck_document_manager_editorial_draft_sha256", "source_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_document_manager_editorial_draft_status", "status IN ('pending_review', 'in_review', 'approved', 'rejected')");
                    table.CheckConstraint("ck_document_manager_editorial_draft_title_origin", "title_origin IN ('original_filename', 'imported', 'ai_proposed', 'editorial')");
                    table.CheckConstraint("ck_document_manager_editorial_draft_update_time", "updated_at_utc >= created_at_utc");
                    table.CheckConstraint("ck_document_manager_editorial_draft_version", "version >= 0");
                    table.ForeignKey(
                        name: "FK_document_manager_editorial_drafts_document_manager_submissi~",
                        columns: x => new { x.submission_id, x.manifest_revision },
                        principalTable: "document_manager_submission_manifest_inbox",
                        principalColumns: new[] { "submission_id", "revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_manager_editorial_draft_parts",
                columns: table => new
                {
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processing_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    result_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    scope_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    start_physical_page_number = table.Column<int>(type: "integer", nullable: true),
                    end_physical_page_number = table.Column<int>(type: "integer", nullable: true),
                    scope_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    start_content_unit_index = table.Column<int>(type: "integer", nullable: true),
                    start_content_unit_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    end_content_unit_index = table.Column<int>(type: "integer", nullable: true),
                    end_content_unit_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_manager_editorial_draft_parts", x => new { x.draft_id, x.processing_unit_id });
                    table.CheckConstraint("ck_document_manager_editorial_draft_part_ordinal", "ordinal > 0");
                    table.ForeignKey(
                        name: "FK_document_manager_editorial_draft_parts_document_manager_edi~",
                        column: x => x.draft_id,
                        principalTable: "document_manager_editorial_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_manager_editorial_draft_parts_document_manager_res~",
                        column: x => x.result_reference,
                        principalTable: "document_manager_result_inbox",
                        principalColumn: "result_reference",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_manager_editorial_draft_part_result",
                table: "document_manager_editorial_draft_parts",
                column: "result_reference");

            migrationBuilder.CreateIndex(
                name: "ux_document_manager_editorial_draft_part_ordinal",
                table: "document_manager_editorial_draft_parts",
                columns: new[] { "draft_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_manager_editorial_draft_review_queue",
                table: "document_manager_editorial_drafts",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_document_manager_editorial_draft_manifest",
                table: "document_manager_editorial_drafts",
                columns: new[] { "submission_id", "manifest_revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_manager_editorial_draft_parts");

            migrationBuilder.DropTable(
                name: "document_manager_editorial_drafts");
        }
    }
}
