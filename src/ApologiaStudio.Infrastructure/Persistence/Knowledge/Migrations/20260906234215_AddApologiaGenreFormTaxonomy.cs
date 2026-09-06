using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddApologiaGenreFormTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "apologia_genre_form_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    preferred_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    definition = table.Column<string>(type: "text", nullable: false),
                    prediction_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    taxonomy_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apologia_genre_form_terms", x => x.id);
                    table.CheckConstraint("ck_apologia_genre_form_term_prediction_mode", "prediction_mode IN ('encoder_predictable', 'manual_only')");
                    table.CheckConstraint("ck_apologia_genre_form_term_status", "status IN ('active', 'retired')");
                });

            migrationBuilder.CreateIndex(
                name: "ux_apologia_genre_form_terms_code",
                table: "apologia_genre_form_terms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_apologia_genre_form_terms_order",
                table: "apologia_genre_form_terms",
                columns: new[] { "taxonomy_version", "display_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apologia_genre_form_terms");
        }
    }
}
