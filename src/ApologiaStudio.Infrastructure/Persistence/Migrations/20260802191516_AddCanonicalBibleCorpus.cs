using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalBibleCorpus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bible_books",
                columns: table => new
                {
                    usfm_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    osis_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    canonical_order = table.Column<int>(type: "integer", nullable: false),
                    canon_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_books", x => x.usfm_code);
                    table.CheckConstraint("ck_bible_books_canonical_order_positive", "canonical_order > 0");
                });

            migrationBuilder.CreateTable(
                name: "bible_editions",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    language_tag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    canon_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_editions", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "bible_corpus_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edition_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    upstream_revision = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source_tree_sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    import_fingerprint = table.Column<string>(type: "character(64)", nullable: false),
                    parser_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parser_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    normalization_policy_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    canonical_schema_version = table.Column<int>(type: "integer", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_corpus_versions", x => x.id);
                    table.CheckConstraint("ck_bible_corpus_versions_approval", "(approved_at IS NULL OR approved_at >= imported_at) AND (NOT is_active OR (approved_at IS NOT NULL AND validation_status = 'approved'))");
                    table.CheckConstraint("ck_bible_corpus_versions_import_fingerprint", "import_fingerprint ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_bible_corpus_versions_schema_version_positive", "canonical_schema_version > 0");
                    table.CheckConstraint("ck_bible_corpus_versions_source_tree_sha256", "source_tree_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_bible_corpus_versions_validation_status", "validation_status IN ('pending', 'validated', 'approved', 'failed')");
                    table.ForeignKey(
                        name: "FK_bible_corpus_versions_bible_editions_edition_code",
                        column: x => x.edition_code,
                        principalTable: "bible_editions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bible_corpus_books",
                columns: table => new
                {
                    corpus_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usfm_book_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    book_ordinal = table.Column<int>(type: "integer", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_relative_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_corpus_books", x => new { x.corpus_version_id, x.usfm_book_code });
                    table.CheckConstraint("ck_bible_corpus_books_book_ordinal_positive", "book_ordinal > 0");
                    table.ForeignKey(
                        name: "FK_bible_corpus_books_bible_books_usfm_book_code",
                        column: x => x.usfm_book_code,
                        principalTable: "bible_books",
                        principalColumn: "usfm_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bible_corpus_books_bible_corpus_versions_corpus_version_id",
                        column: x => x.corpus_version_id,
                        principalTable: "bible_corpus_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bible_source_artifacts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    corpus_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_uri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false),
                    downloaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_source_artifacts", x => x.id);
                    table.CheckConstraint("ck_bible_source_artifacts_byte_length_positive", "byte_length > 0");
                    table.CheckConstraint("ck_bible_source_artifacts_role", "role IN ('canonical-usfm', 'validation-vpl', 'validation-report')");
                    table.CheckConstraint("ck_bible_source_artifacts_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_bible_source_artifacts_bible_corpus_versions_corpus_version~",
                        column: x => x.corpus_version_id,
                        principalTable: "bible_corpus_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bible_verses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    corpus_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usfm_book_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    chapter_number = table.Column<int>(type: "integer", nullable: false),
                    verse_label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verse_ordinal = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    source_relative_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_line = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_verses", x => x.id);
                    table.CheckConstraint("ck_bible_verses_chapter_positive", "chapter_number > 0");
                    table.CheckConstraint("ck_bible_verses_ordinal_positive", "verse_ordinal > 0");
                    table.CheckConstraint("ck_bible_verses_source_line_positive", "source_line > 0");
                    table.ForeignKey(
                        name: "FK_bible_verses_bible_corpus_books_corpus_version_id_usfm_book~",
                        columns: x => new { x.corpus_version_id, x.usfm_book_code },
                        principalTable: "bible_corpus_books",
                        principalColumns: new[] { "corpus_version_id", "usfm_book_code" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bible_supplemental_texts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    verse_id = table.Column<long>(type: "bigint", nullable: false),
                    source_ordinal = table.Column<int>(type: "integer", nullable: false),
                    marker = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    placement = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    character_offset = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_supplemental_texts", x => x.id);
                    table.CheckConstraint("ck_bible_supplemental_texts_marker", "marker IN ('d', 'sp')");
                    table.CheckConstraint("ck_bible_supplemental_texts_offset", "(placement = 'Within' AND character_offset >= 0) OR (placement IN ('Before', 'After') AND character_offset IS NULL)");
                    table.CheckConstraint("ck_bible_supplemental_texts_ordinal_positive", "source_ordinal > 0");
                    table.ForeignKey(
                        name: "FK_bible_supplemental_texts_bible_verses_verse_id",
                        column: x => x.verse_id,
                        principalTable: "bible_verses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bible_word_annotations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    verse_id = table.Column<long>(type: "bigint", nullable: false),
                    source_ordinal = table.Column<int>(type: "integer", nullable: false),
                    marker = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attribute_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    attribute_value = table.Column<string>(type: "text", nullable: false),
                    character_offset = table.Column<int>(type: "integer", nullable: false),
                    character_length = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bible_word_annotations", x => x.id);
                    table.CheckConstraint("ck_bible_word_annotations_length_positive", "character_length > 0");
                    table.CheckConstraint("ck_bible_word_annotations_offset_nonnegative", "character_offset >= 0");
                    table.CheckConstraint("ck_bible_word_annotations_ordinal_positive", "source_ordinal > 0");
                    table.ForeignKey(
                        name: "FK_bible_word_annotations_bible_verses_verse_id",
                        column: x => x.verse_id,
                        principalTable: "bible_verses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "bible_books",
                columns: new[] { "usfm_code", "canon_code", "canonical_order", "osis_code" },
                values: new object[,]
                {
                    { "1CH", "protestant-66", 13, "1Chr" },
                    { "1CO", "protestant-66", 46, "1Cor" },
                    { "1JN", "protestant-66", 62, "1John" },
                    { "1KI", "protestant-66", 11, "1Kgs" },
                    { "1PE", "protestant-66", 60, "1Pet" },
                    { "1SA", "protestant-66", 9, "1Sam" },
                    { "1TH", "protestant-66", 52, "1Thess" },
                    { "1TI", "protestant-66", 54, "1Tim" },
                    { "2CH", "protestant-66", 14, "2Chr" },
                    { "2CO", "protestant-66", 47, "2Cor" },
                    { "2JN", "protestant-66", 63, "2John" },
                    { "2KI", "protestant-66", 12, "2Kgs" },
                    { "2PE", "protestant-66", 61, "2Pet" },
                    { "2SA", "protestant-66", 10, "2Sam" },
                    { "2TH", "protestant-66", 53, "2Thess" },
                    { "2TI", "protestant-66", 55, "2Tim" },
                    { "3JN", "protestant-66", 64, "3John" },
                    { "ACT", "protestant-66", 44, "Acts" },
                    { "AMO", "protestant-66", 30, "Amos" },
                    { "COL", "protestant-66", 51, "Col" },
                    { "DAN", "protestant-66", 27, "Dan" },
                    { "DEU", "protestant-66", 5, "Deut" },
                    { "ECC", "protestant-66", 21, "Eccl" },
                    { "EPH", "protestant-66", 49, "Eph" },
                    { "EST", "protestant-66", 17, "Esth" },
                    { "EXO", "protestant-66", 2, "Exod" },
                    { "EZK", "protestant-66", 26, "Ezek" },
                    { "EZR", "protestant-66", 15, "Ezra" },
                    { "GAL", "protestant-66", 48, "Gal" },
                    { "GEN", "protestant-66", 1, "Gen" },
                    { "HAB", "protestant-66", 35, "Hab" },
                    { "HAG", "protestant-66", 37, "Hag" },
                    { "HEB", "protestant-66", 58, "Heb" },
                    { "HOS", "protestant-66", 28, "Hos" },
                    { "ISA", "protestant-66", 23, "Isa" },
                    { "JAS", "protestant-66", 59, "Jas" },
                    { "JDG", "protestant-66", 7, "Judg" },
                    { "JER", "protestant-66", 24, "Jer" },
                    { "JHN", "protestant-66", 43, "John" },
                    { "JOB", "protestant-66", 18, "Job" },
                    { "JOL", "protestant-66", 29, "Joel" },
                    { "JON", "protestant-66", 32, "Jonah" },
                    { "JOS", "protestant-66", 6, "Josh" },
                    { "JUD", "protestant-66", 65, "Jude" },
                    { "LAM", "protestant-66", 25, "Lam" },
                    { "LEV", "protestant-66", 3, "Lev" },
                    { "LUK", "protestant-66", 42, "Luke" },
                    { "MAL", "protestant-66", 39, "Mal" },
                    { "MAT", "protestant-66", 40, "Matt" },
                    { "MIC", "protestant-66", 33, "Mic" },
                    { "MRK", "protestant-66", 41, "Mark" },
                    { "NAM", "protestant-66", 34, "Nah" },
                    { "NEH", "protestant-66", 16, "Neh" },
                    { "NUM", "protestant-66", 4, "Num" },
                    { "OBA", "protestant-66", 31, "Obad" },
                    { "PHM", "protestant-66", 57, "Phlm" },
                    { "PHP", "protestant-66", 50, "Phil" },
                    { "PRO", "protestant-66", 20, "Prov" },
                    { "PSA", "protestant-66", 19, "Ps" },
                    { "REV", "protestant-66", 66, "Rev" },
                    { "ROM", "protestant-66", 45, "Rom" },
                    { "RUT", "protestant-66", 8, "Ruth" },
                    { "SNG", "protestant-66", 22, "Song" },
                    { "TIT", "protestant-66", 56, "Titus" },
                    { "ZEC", "protestant-66", 38, "Zech" },
                    { "ZEP", "protestant-66", 36, "Zeph" }
                });

            migrationBuilder.CreateIndex(
                name: "ux_bible_books_canonical_order",
                table: "bible_books",
                column: "canonical_order",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bible_books_osis_code",
                table: "bible_books",
                column: "osis_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bible_corpus_books_usfm_book_code",
                table: "bible_corpus_books",
                column: "usfm_book_code");

            migrationBuilder.CreateIndex(
                name: "ux_bible_corpus_books_version_ordinal",
                table: "bible_corpus_books",
                columns: new[] { "corpus_version_id", "book_ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bible_corpus_versions_edition_imported_at",
                table: "bible_corpus_versions",
                columns: new[] { "edition_code", "imported_at" });

            migrationBuilder.CreateIndex(
                name: "ux_bible_corpus_versions_active_edition",
                table: "bible_corpus_versions",
                column: "edition_code",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ux_bible_corpus_versions_import_fingerprint",
                table: "bible_corpus_versions",
                column: "import_fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bible_source_artifacts_version_role_file",
                table: "bible_source_artifacts",
                columns: new[] { "corpus_version_id", "role", "file_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bible_supplemental_texts_verse_ordinal",
                table: "bible_supplemental_texts",
                columns: new[] { "verse_id", "source_ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bible_verses_order",
                table: "bible_verses",
                columns: new[] { "corpus_version_id", "usfm_book_code", "chapter_number", "verse_ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bible_verses_reference",
                table: "bible_verses",
                columns: new[] { "corpus_version_id", "usfm_book_code", "chapter_number", "verse_label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bible_word_annotations_verse_ordinal",
                table: "bible_word_annotations",
                columns: new[] { "verse_id", "source_ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bible_source_artifacts");

            migrationBuilder.DropTable(
                name: "bible_supplemental_texts");

            migrationBuilder.DropTable(
                name: "bible_word_annotations");

            migrationBuilder.DropTable(
                name: "bible_verses");

            migrationBuilder.DropTable(
                name: "bible_corpus_books");

            migrationBuilder.DropTable(
                name: "bible_books");

            migrationBuilder.DropTable(
                name: "bible_corpus_versions");

            migrationBuilder.DropTable(
                name: "bible_editions");
        }
    }
}
