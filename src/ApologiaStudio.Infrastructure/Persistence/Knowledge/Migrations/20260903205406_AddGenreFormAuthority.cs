using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddGenreFormAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genre_form_authority_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_uri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    importer_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    term_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_form_authority_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "genre_form_authority_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    authority_identifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    authority_uri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    preferred_label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    language_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    authority_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_form_authority_terms", x => x.id);
                    table.CheckConstraint("ck_genre_form_authority_term_status", "authority_status IN ('active', 'deprecated')");
                    table.ForeignKey(
                        name: "FK_genre_form_authority_terms_genre_form_authority_snapshots_s~",
                        column: x => x.snapshot_id,
                        principalTable: "genre_form_authority_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "genre_form_authority_notes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_form_authority_notes", x => x.id);
                    table.CheckConstraint("ck_genre_form_authority_note_type", "note_type IN ('general', 'history', 'example')");
                    table.ForeignKey(
                        name: "FK_genre_form_authority_notes_genre_form_authority_terms_term_~",
                        column: x => x.term_id,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "genre_form_authority_variants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    language_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_form_authority_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_genre_form_authority_variants_genre_form_authority_terms_te~",
                        column: x => x.term_id,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "genre_form_broader_relations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    narrower_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    broader_term_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_form_broader_relations", x => x.id);
                    table.CheckConstraint("ck_genre_form_broader_distinct", "narrower_term_id <> broader_term_id");
                    table.ForeignKey(
                        name: "FK_genre_form_broader_relations_genre_form_authority_terms_bro~",
                        column: x => x.broader_term_id,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_genre_form_broader_relations_genre_form_authority_terms_nar~",
                        column: x => x.narrower_term_id,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "genre_form_profile_entries",
                columns: table => new
                {
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usage_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    profile_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "genre_form_related_relations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    term_id_a = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id_b = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genre_form_related_relations", x => x.id);
                    table.CheckConstraint("ck_genre_form_related_canonical", "term_id_a < term_id_b");
                    table.ForeignKey(
                        name: "FK_genre_form_related_relations_genre_form_authority_terms_ter~",
                        column: x => x.term_id_a,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_genre_form_related_relations_genre_form_authority_terms_te~1",
                        column: x => x.term_id_b,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_work_genre_forms",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_work_genre_forms", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_work_genre_forms_genre_form_authority_terms_term_~",
                        column: x => x.term_id,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_work_genre_forms_knowledge_works_work_id",
                        column: x => x.work_id,
                        principalTable: "knowledge_works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_genre_form_authority_notes",
                table: "genre_form_authority_notes",
                columns: new[] { "term_id", "note_type" });

            migrationBuilder.CreateIndex(
                name: "ux_genre_form_authority_snapshots_content",
                table: "genre_form_authority_snapshots",
                columns: new[] { "authority", "content_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_genre_form_authority_terms_snapshot_id",
                table: "genre_form_authority_terms",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_genre_form_authority_terms_status",
                table: "genre_form_authority_terms",
                columns: new[] { "authority", "authority_status" });

            migrationBuilder.CreateIndex(
                name: "ux_genre_form_authority_terms_uri",
                table: "genre_form_authority_terms",
                column: "authority_uri",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_genre_form_authority_variants",
                table: "genre_form_authority_variants",
                columns: new[] { "term_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_genre_form_broader_relations_broader",
                table: "genre_form_broader_relations",
                column: "broader_term_id");

            migrationBuilder.CreateIndex(
                name: "ux_genre_form_broader_relations",
                table: "genre_form_broader_relations",
                columns: new[] { "narrower_term_id", "broader_term_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_genre_form_profile_entries_usage",
                table: "genre_form_profile_entries",
                column: "usage_status");

            migrationBuilder.CreateIndex(
                name: "IX_genre_form_related_relations_term_id_b",
                table: "genre_form_related_relations",
                column: "term_id_b");

            migrationBuilder.CreateIndex(
                name: "ux_genre_form_related_relations",
                table: "genre_form_related_relations",
                columns: new[] { "term_id_a", "term_id_b" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_work_genre_forms_term_id",
                table: "knowledge_work_genre_forms",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_work_genre_forms",
                table: "knowledge_work_genre_forms",
                columns: new[] { "work_id", "term_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genre_form_authority_notes");

            migrationBuilder.DropTable(
                name: "genre_form_authority_variants");

            migrationBuilder.DropTable(
                name: "genre_form_broader_relations");

            migrationBuilder.DropTable(
                name: "genre_form_profile_entries");

            migrationBuilder.DropTable(
                name: "genre_form_related_relations");

            migrationBuilder.DropTable(
                name: "knowledge_work_genre_forms");

            migrationBuilder.DropTable(
                name: "genre_form_authority_terms");

            migrationBuilder.DropTable(
                name: "genre_form_authority_snapshots");
        }
    }
}
