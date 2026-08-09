using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserManagedAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_built_in",
                table: "ai_agent_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "ai_agent_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "routing_description",
                table: "ai_agent_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "ai_agent_settings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_settings_slug",
                table: "ai_agent_settings",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ai_agent_settings_slug",
                table: "ai_agent_settings");

            migrationBuilder.DropColumn(
                name: "is_built_in",
                table: "ai_agent_settings");

            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "ai_agent_settings");

            migrationBuilder.DropColumn(
                name: "routing_description",
                table: "ai_agent_settings");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "ai_agent_settings");
        }
    }
}
