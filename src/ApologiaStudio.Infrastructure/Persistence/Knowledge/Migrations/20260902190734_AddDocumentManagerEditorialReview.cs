using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentManagerEditorialReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_edited_by_user_id",
                table: "document_manager_editorial_drafts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "primary_contributor_name",
                table: "document_manager_editorial_drafts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "primary_contributor_role",
                table: "document_manager_editorial_drafts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "document_manager_editorial_drafts",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at_utc",
                table: "document_manager_editorial_drafts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_user_id",
                table: "document_manager_editorial_drafts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "document_manager_editorial_review_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_manager_editorial_review_events", x => x.id);
                    table.CheckConstraint("ck_document_manager_editorial_review_event_action", "action IN ('save', 'approve', 'reject')");
                    table.CheckConstraint("ck_document_manager_editorial_review_event_from_status", "from_status IN ('pending_review', 'in_review')");
                    table.CheckConstraint("ck_document_manager_editorial_review_event_to_status", "to_status IN ('in_review', 'approved', 'rejected')");
                    table.CheckConstraint("ck_document_manager_editorial_review_event_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_document_manager_editorial_review_events_document_manager_e~",
                        column: x => x.draft_id,
                        principalTable: "document_manager_editorial_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_draft_contributor",
                table: "document_manager_editorial_drafts",
                sql: "(primary_contributor_name IS NULL) = (primary_contributor_role IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_draft_rejection",
                table: "document_manager_editorial_drafts",
                sql: "(status = 'rejected' AND rejection_reason IS NOT NULL) OR (status <> 'rejected' AND rejection_reason IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_draft_review_decision",
                table: "document_manager_editorial_drafts",
                sql: "((status IN ('approved', 'rejected')) AND reviewed_by_user_id IS NOT NULL AND reviewed_at_utc IS NOT NULL) OR ((status IN ('pending_review', 'in_review')) AND reviewed_by_user_id IS NULL AND reviewed_at_utc IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ux_document_manager_editorial_review_event_version",
                table: "document_manager_editorial_review_events",
                columns: new[] { "draft_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_manager_editorial_review_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_draft_contributor",
                table: "document_manager_editorial_drafts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_draft_rejection",
                table: "document_manager_editorial_drafts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_draft_review_decision",
                table: "document_manager_editorial_drafts");

            migrationBuilder.DropColumn(
                name: "last_edited_by_user_id",
                table: "document_manager_editorial_drafts");

            migrationBuilder.DropColumn(
                name: "primary_contributor_name",
                table: "document_manager_editorial_drafts");

            migrationBuilder.DropColumn(
                name: "primary_contributor_role",
                table: "document_manager_editorial_drafts");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "document_manager_editorial_drafts");

            migrationBuilder.DropColumn(
                name: "reviewed_at_utc",
                table: "document_manager_editorial_drafts");

            migrationBuilder.DropColumn(
                name: "reviewed_by_user_id",
                table: "document_manager_editorial_drafts");
        }
    }
}
