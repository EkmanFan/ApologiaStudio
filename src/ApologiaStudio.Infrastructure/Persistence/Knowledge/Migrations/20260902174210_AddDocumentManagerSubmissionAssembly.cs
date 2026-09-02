using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentManagerSubmissionAssembly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_manager_submission_manifest_inbox",
                columns: table => new
                {
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    source_sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    finalized_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_manager_submission_manifest_inbox", x => new { x.submission_id, x.revision });
                    table.CheckConstraint("ck_document_manager_submission_manifest_revision", "revision > 0");
                    table.CheckConstraint("ck_document_manager_submission_manifest_sha256", "source_sha256 ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "document_manager_expected_unit_inbox",
                columns: table => new
                {
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manifest_revision = table.Column<int>(type: "integer", nullable: false),
                    processing_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_document_manager_expected_unit_inbox", x => new { x.submission_id, x.manifest_revision, x.processing_unit_id });
                    table.CheckConstraint("ck_document_manager_expected_unit_ordinal", "ordinal > 0");
                    table.ForeignKey(
                        name: "FK_document_manager_expected_unit_inbox_document_manager_submi~",
                        columns: x => new { x.submission_id, x.manifest_revision },
                        principalTable: "document_manager_submission_manifest_inbox",
                        principalColumns: new[] { "submission_id", "revision" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_document_manager_expected_unit_ordinal",
                table: "document_manager_expected_unit_inbox",
                columns: new[] { "submission_id", "manifest_revision", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_manager_submission_manifest_latest",
                table: "document_manager_submission_manifest_inbox",
                columns: new[] { "submission_id", "revision" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_manager_expected_unit_inbox");

            migrationBuilder.DropTable(
                name: "document_manager_submission_manifest_inbox");
        }
    }
}
