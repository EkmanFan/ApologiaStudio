using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentManagerEditorialAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_review_event_action",
                table: "document_manager_editorial_review_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_review_event_from_status",
                table: "document_manager_editorial_review_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_review_event_to_status",
                table: "document_manager_editorial_review_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_review_event_action",
                table: "document_manager_editorial_review_events",
                sql: "action IN ('save', 'approve', 'reject', 'reopen')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_review_event_from_status",
                table: "document_manager_editorial_review_events",
                sql: "from_status IN ('pending_review', 'in_review', 'rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_review_event_to_status",
                table: "document_manager_editorial_review_events",
                sql: "to_status IN ('pending_review', 'in_review', 'approved', 'rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_review_event_action",
                table: "document_manager_editorial_review_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_review_event_from_status",
                table: "document_manager_editorial_review_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_manager_editorial_review_event_to_status",
                table: "document_manager_editorial_review_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_review_event_action",
                table: "document_manager_editorial_review_events",
                sql: "action IN ('save', 'approve', 'reject')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_review_event_from_status",
                table: "document_manager_editorial_review_events",
                sql: "from_status IN ('pending_review', 'in_review')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_manager_editorial_review_event_to_status",
                table: "document_manager_editorial_review_events",
                sql: "to_status IN ('in_review', 'approved', 'rejected')");
        }
    }
}
