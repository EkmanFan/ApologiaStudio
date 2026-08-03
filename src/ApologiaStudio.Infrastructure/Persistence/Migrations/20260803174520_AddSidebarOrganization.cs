using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSidebarOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversations_owner_created_at",
                table: "conversations");

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                table: "conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "conversation_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_projects", x => x.id);
                    table.CheckConstraint("ck_conversation_projects_sort_order", "sort_order >= 0");
                });

            migrationBuilder.CreateTable(
                name: "sidebar_pins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sidebar_pins", x => x.id);
                    table.CheckConstraint("ck_sidebar_pins_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_sidebar_pins_target", "(target_kind = 'Conversation' AND conversation_id IS NOT NULL AND project_id IS NULL) OR (target_kind = 'Project' AND project_id IS NOT NULL AND conversation_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_sidebar_pins_conversation_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "conversation_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sidebar_pins_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_owner_project_sort_order",
                table: "conversations",
                columns: new[] { "owner_id", "project_id", "sort_order", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_project_id",
                table: "conversations",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_projects_owner_sort_order",
                table: "conversation_projects",
                columns: new[] { "owner_id", "sort_order", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_conversation_projects_owner_name",
                table: "conversation_projects",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sidebar_pins_conversation_id",
                table: "sidebar_pins",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_sidebar_pins_owner_sort_order",
                table: "sidebar_pins",
                columns: new[] { "owner_id", "sort_order", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sidebar_pins_project_id",
                table: "sidebar_pins",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_sidebar_pins_owner_conversation",
                table: "sidebar_pins",
                columns: new[] { "owner_id", "conversation_id" },
                unique: true,
                filter: "conversation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_sidebar_pins_owner_project",
                table: "sidebar_pins",
                columns: new[] { "owner_id", "project_id" },
                unique: true,
                filter: "project_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_conversations_conversation_projects_project_id",
                table: "conversations",
                column: "project_id",
                principalTable: "conversation_projects",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_conversations_conversation_projects_project_id",
                table: "conversations");

            migrationBuilder.DropTable(
                name: "sidebar_pins");

            migrationBuilder.DropTable(
                name: "conversation_projects");

            migrationBuilder.DropIndex(
                name: "ix_conversations_owner_project_sort_order",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_conversations_project_id",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "conversations");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_owner_created_at",
                table: "conversations",
                columns: new[] { "owner_id", "created_at" });
        }
    }
}
