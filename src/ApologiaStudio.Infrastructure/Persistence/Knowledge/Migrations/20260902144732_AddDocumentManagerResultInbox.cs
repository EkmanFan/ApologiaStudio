using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentManagerResultInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_manager_result_inbox",
                columns: table => new
                {
                    result_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processing_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    start_physical_page_number = table.Column<int>(type: "integer", nullable: true),
                    end_physical_page_number = table.Column<int>(type: "integer", nullable: true),
                    scope_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    start_content_unit_index = table.Column<int>(type: "integer", nullable: true),
                    start_content_unit_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    end_content_unit_index = table.Column<int>(type: "integer", nullable: true),
                    end_content_unit_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    schema_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    media_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    available_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_manager_result_inbox", x => x.result_reference);
                    table.CheckConstraint("ck_document_manager_result_inbox_length", "byte_length > 0");
                    table.CheckConstraint("ck_document_manager_result_inbox_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "document_manager_visual_asset_inbox",
                columns: table => new
                {
                    result_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    asset_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    media_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    payload = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_manager_visual_asset_inbox", x => new { x.result_reference, x.asset_id });
                    table.CheckConstraint("ck_document_manager_visual_asset_inbox_length", "byte_length > 0");
                    table.CheckConstraint("ck_document_manager_visual_asset_inbox_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_document_manager_visual_asset_inbox_document_manager_result~",
                        column: x => x.result_reference,
                        principalTable: "document_manager_result_inbox",
                        principalColumn: "result_reference",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_manager_result_inbox_processing_unit",
                table: "document_manager_result_inbox",
                column: "processing_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_manager_result_inbox_received",
                table: "document_manager_result_inbox",
                column: "received_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_manager_visual_asset_inbox");

            migrationBuilder.DropTable(
                name: "document_manager_result_inbox");
        }
    }
}
