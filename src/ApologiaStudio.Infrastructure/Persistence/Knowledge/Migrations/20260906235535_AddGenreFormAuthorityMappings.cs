using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddGenreFormAuthorityMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genre_form_authority_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    external_concept_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    external_concept_uri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    mapping_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_form_authority_mappings", x => x.id);
                    table.CheckConstraint("ck_genre_form_authority_mapping_authority", "authority IN ('lcgft', 'bnf')");
                    table.CheckConstraint("ck_genre_form_authority_mapping_kind", "mapping_kind IN ('exact', 'broader', 'narrower', 'close', 'related')");
                    table.ForeignKey(
                        name: "FK_genre_form_authority_mappings_apologia_genre_form_terms_pro~",
                        column: x => x.product_term_id,
                        principalTable: "apologia_genre_form_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_genre_form_authority_mappings",
                table: "genre_form_authority_mappings",
                columns: new[] { "product_term_id", "authority", "external_concept_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genre_form_authority_mappings");
        }
    }
}
