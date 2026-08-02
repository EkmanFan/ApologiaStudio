using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLanguagePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interface_language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    theological_language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_preferences", x => x.user_id);
                    table.CheckConstraint("ck_user_preferences_interface_language", "interface_language IN ('French', 'English')");
                    table.CheckConstraint("ck_user_preferences_theological_language", "theological_language IS NULL OR theological_language IN ('French', 'English')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_preferences");
        }
    }
}
