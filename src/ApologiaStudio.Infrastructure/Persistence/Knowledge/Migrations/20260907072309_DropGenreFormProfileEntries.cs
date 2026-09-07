using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class DropGenreFormProfileEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genre_form_profile_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genre_form_profile_entries",
                columns: table => new
                {
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    profile_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usage_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_form_profile_entries", x => x.term_id);
                    table.CheckConstraint("ck_genre_form_profile_usage", "usage_status IN ('excluded', 'structural_only', 'selectable')");
                    table.ForeignKey(
                        name: "FK_genre_form_profile_entries_genre_form_authority_terms_term_~",
                        column: x => x.term_id,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_genre_form_profile_entries_usage",
                table: "genre_form_profile_entries",
                column: "usage_status");
        }
    }
}
