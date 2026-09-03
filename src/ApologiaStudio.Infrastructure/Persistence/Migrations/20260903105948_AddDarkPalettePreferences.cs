using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDarkPalettePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dark_page_color",
                table: "user_preferences",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#242424");

            migrationBuilder.AddColumn<string>(
                name: "dark_surface_color",
                table: "user_preferences",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#303030");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_preferences_dark_page_color",
                table: "user_preferences",
                sql: "dark_page_color ~ '^#[0-9A-F]{6}$' AND substring(dark_page_color from 2 for 2) = substring(dark_page_color from 4 for 2) AND substring(dark_page_color from 4 for 2) = substring(dark_page_color from 6 for 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_preferences_dark_surface_color",
                table: "user_preferences",
                sql: "dark_surface_color ~ '^#[0-9A-F]{6}$' AND substring(dark_surface_color from 2 for 2) = substring(dark_surface_color from 4 for 2) AND substring(dark_surface_color from 4 for 2) = substring(dark_surface_color from 6 for 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_preferences_dark_page_color",
                table: "user_preferences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_preferences_dark_surface_color",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "dark_page_color",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "dark_surface_color",
                table: "user_preferences");
        }
    }
}
