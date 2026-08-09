using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComposerEnterBehaviorPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "composer_enter_behavior",
                table: "user_preferences",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValueSql: "'NewLine'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_preferences_composer_enter_behavior",
                table: "user_preferences",
                sql: "composer_enter_behavior IN ('NewLine', 'SendMessage')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_preferences_composer_enter_behavior",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "composer_enter_behavior",
                table: "user_preferences");
        }
    }
}
