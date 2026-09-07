using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddEncoderSuggestionProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "justification",
                table: "metadata_review_suggestions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<double>(
                name: "score",
                table: "metadata_review_suggestions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model_version",
                table: "metadata_review_analyses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plan_id",
                table: "metadata_review_analyses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "score",
                table: "metadata_review_suggestions");

            migrationBuilder.DropColumn(
                name: "model_version",
                table: "metadata_review_analyses");

            migrationBuilder.DropColumn(
                name: "plan_id",
                table: "metadata_review_analyses");

            migrationBuilder.AlterColumn<string>(
                name: "justification",
                table: "metadata_review_suggestions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
