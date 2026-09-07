using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class CutOverWorkGenreFormsToProductTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_work_genre_forms_genre_form_authority_terms_term_~",
                table: "knowledge_work_genre_forms");

            migrationBuilder.RenameColumn(
                name: "term_id",
                table: "knowledge_work_genre_forms",
                newName: "product_term_id");

            migrationBuilder.RenameIndex(
                name: "IX_knowledge_work_genre_forms_term_id",
                table: "knowledge_work_genre_forms",
                newName: "IX_knowledge_work_genre_forms_product_term_id");

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_work_genre_forms_apologia_genre_form_terms_produc~",
                table: "knowledge_work_genre_forms",
                column: "product_term_id",
                principalTable: "apologia_genre_form_terms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_work_genre_forms_apologia_genre_form_terms_produc~",
                table: "knowledge_work_genre_forms");

            migrationBuilder.RenameColumn(
                name: "product_term_id",
                table: "knowledge_work_genre_forms",
                newName: "term_id");

            migrationBuilder.RenameIndex(
                name: "IX_knowledge_work_genre_forms_product_term_id",
                table: "knowledge_work_genre_forms",
                newName: "IX_knowledge_work_genre_forms_term_id");

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_work_genre_forms_genre_form_authority_terms_term_~",
                table: "knowledge_work_genre_forms",
                column: "term_id",
                principalTable: "genre_form_authority_terms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
