using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserThemePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "theme_color",
                table: "user_preferences",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#2D766E");

            migrationBuilder.AddColumn<string>(
                name: "theme_mode",
                table: "user_preferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValueSql: "'Light'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_preferences_theme_color",
                table: "user_preferences",
                sql: "theme_color ~ '^#[0-9A-F]{6}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_preferences_theme_mode",
                table: "user_preferences",
                sql: "theme_mode IN ('Light', 'Dark')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_preferences_theme_color",
                table: "user_preferences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_preferences_theme_mode",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "theme_color",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "theme_mode",
                table: "user_preferences");
        }
    }
}
