using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class CutOverReviewGenreFormsToProductTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_document_manager_editorial_draft_genre_forms_genre_form_aut~",
                table: "document_manager_editorial_draft_genre_forms");

            migrationBuilder.DropForeignKey(
                name: "FK_metadata_review_suggestions_genre_form_authority_terms_term~",
                table: "metadata_review_suggestions");

            migrationBuilder.RenameColumn(
                name: "term_id",
                table: "metadata_review_suggestions",
                newName: "product_term_id");

            migrationBuilder.RenameIndex(
                name: "IX_metadata_review_suggestions_term_id",
                table: "metadata_review_suggestions",
                newName: "IX_metadata_review_suggestions_product_term_id");

            migrationBuilder.RenameColumn(
                name: "term_id",
                table: "document_manager_editorial_draft_genre_forms",
                newName: "product_term_id");

            migrationBuilder.RenameIndex(
                name: "IX_document_manager_editorial_draft_genre_forms_term_id",
                table: "document_manager_editorial_draft_genre_forms",
                newName: "IX_document_manager_editorial_draft_genre_forms_product_term_id");

            migrationBuilder.AddForeignKey(
                name: "FK_document_manager_editorial_draft_genre_forms_apologia_genre~",
                table: "document_manager_editorial_draft_genre_forms",
                column: "product_term_id",
                principalTable: "apologia_genre_form_terms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_metadata_review_suggestions_apologia_genre_form_terms_produ~",
                table: "metadata_review_suggestions",
                column: "product_term_id",
                principalTable: "apologia_genre_form_terms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_document_manager_editorial_draft_genre_forms_apologia_genre~",
                table: "document_manager_editorial_draft_genre_forms");

            migrationBuilder.DropForeignKey(
                name: "FK_metadata_review_suggestions_apologia_genre_form_terms_produ~",
                table: "metadata_review_suggestions");

            migrationBuilder.RenameColumn(
                name: "product_term_id",
                table: "metadata_review_suggestions",
                newName: "term_id");

            migrationBuilder.RenameIndex(
                name: "IX_metadata_review_suggestions_product_term_id",
                table: "metadata_review_suggestions",
                newName: "IX_metadata_review_suggestions_term_id");

            migrationBuilder.RenameColumn(
                name: "product_term_id",
                table: "document_manager_editorial_draft_genre_forms",
                newName: "term_id");

            migrationBuilder.RenameIndex(
                name: "IX_document_manager_editorial_draft_genre_forms_product_term_id",
                table: "document_manager_editorial_draft_genre_forms",
                newName: "IX_document_manager_editorial_draft_genre_forms_term_id");

            migrationBuilder.AddForeignKey(
                name: "FK_document_manager_editorial_draft_genre_forms_genre_form_aut~",
                table: "document_manager_editorial_draft_genre_forms",
                column: "term_id",
                principalTable: "genre_form_authority_terms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_metadata_review_suggestions_genre_form_authority_terms_term~",
                table: "metadata_review_suggestions",
                column: "term_id",
                principalTable: "genre_form_authority_terms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
