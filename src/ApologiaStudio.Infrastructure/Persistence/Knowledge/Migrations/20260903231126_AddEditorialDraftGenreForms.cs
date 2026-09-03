using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialDraftGenreForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_manager_editorial_draft_genre_forms",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_manager_editorial_draft_genre_forms", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_manager_editorial_draft_genre_forms_document_manag~",
                        column: x => x.draft_id,
                        principalTable: "document_manager_editorial_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_manager_editorial_draft_genre_forms_genre_form_aut~",
                        column: x => x.term_id,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_manager_editorial_draft_genre_forms_term_id",
                table: "document_manager_editorial_draft_genre_forms",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "ux_document_manager_editorial_draft_genre_forms",
                table: "document_manager_editorial_draft_genre_forms",
                columns: new[] { "draft_id", "term_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_manager_editorial_draft_genre_forms");
        }
    }
}
