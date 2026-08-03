using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoverableConversationDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversations_owner_project_sort_order",
                table: "conversations");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversations_owner_project_sort_order",
                table: "conversations",
                columns: new[] { "owner_id", "deleted_at", "project_id", "sort_order", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversations_owner_project_sort_order",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "conversations");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_owner_project_sort_order",
                table: "conversations",
                columns: new[] { "owner_id", "project_id", "sort_order", "created_at" });
        }
    }
}
