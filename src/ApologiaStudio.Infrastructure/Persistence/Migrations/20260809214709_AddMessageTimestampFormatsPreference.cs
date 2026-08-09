using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageTimestampFormatsPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "message_date_format",
                table: "user_preferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "dd/MM/yyyy");

            migrationBuilder.AddColumn<string>(
                name: "message_time_format",
                table: "user_preferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "HH:mm:ss");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_preferences_message_date_format",
                table: "user_preferences",
                sql: "message_date_format IN ('dd/MM/yyyy', 'MM/dd/yyyy', 'yyyy-MM-dd')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_preferences_message_time_format",
                table: "user_preferences",
                sql: "message_time_format IN ('HH:mm:ss', 'HH:mm', 'hh:mm:ss tt', 'hh:mm tt')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_preferences_message_date_format",
                table: "user_preferences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_preferences_message_time_format",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "message_date_format",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "message_time_format",
                table: "user_preferences");
        }
    }
}
