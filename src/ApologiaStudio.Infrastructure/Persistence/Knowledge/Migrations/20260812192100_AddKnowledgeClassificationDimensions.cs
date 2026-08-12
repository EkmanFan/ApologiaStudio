using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeClassificationDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "segment_kind",
                table: "knowledge_document_segments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "unknown");


        migrationBuilder.Sql(
            """
            UPDATE knowledge_document_segments AS segment
            SET segment_kind = 'main_text'
            FROM knowledge_artifacts AS artifact
            JOIN knowledge_manifestations AS manifestation
              ON manifestation.id = artifact.manifestation_id
            JOIN knowledge_expressions AS expression
              ON expression.id = manifestation.expression_id
            JOIN knowledge_works AS work
              ON work.id = expression.work_id
            WHERE segment.artifact_id = artifact.id
              AND work.title = 'De Decretis (Defence of the Nicene Definition)'
              AND manifestation.citation_label = 'NPNF2-04, De Decretis';
            """);

            migrationBuilder.CreateTable(
                name: "knowledge_epistemic_frameworks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_epistemic_frameworks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_methodological_frameworks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_methodological_frameworks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_epistemic_framework_assertions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    epistemic_framework_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classification_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assertion_origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    asserted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    asserted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    supporting_segment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_assertion_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_epistemic_framework_assertions", x => x.id);
                    table.CheckConstraint("ck_knowledge_epistemic_framework_origin", "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                    table.CheckConstraint("ck_knowledge_epistemic_framework_review", "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                    table.CheckConstraint("ck_knowledge_epistemic_framework_review_time", "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                    table.CheckConstraint("ck_knowledge_epistemic_framework_supersedes", "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
                    table.CheckConstraint("ck_knowledge_epistemic_framework_type", "classification_type IN ('declared', 'analytical')");
                    table.ForeignKey(
                        name: "fk_epistemic_framework_assertion_framework",
                        column: x => x.epistemic_framework_id,
                        principalTable: "knowledge_epistemic_frameworks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_epistemic_framework_assertion_resource",
                        column: x => x.resource_id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_epistemic_framework_assertion_supersedes",
                        column: x => x.supersedes_assertion_id,
                        principalTable: "knowledge_epistemic_framework_assertions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_epistemic_framework_assertion_support_segment",
                        column: x => x.supporting_segment_id,
                        principalTable: "knowledge_document_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_methodological_framework_assertions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    methodological_framework_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classification_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assertion_origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    asserted_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    asserted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    review_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    supporting_segment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_assertion_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_methodological_framework_assertions", x => x.id);
                    table.CheckConstraint("ck_knowledge_methodological_framework_origin", "assertion_origin IN ('imported', 'ai_proposed', 'editorial')");
                    table.CheckConstraint("ck_knowledge_methodological_framework_review", "review_status IN ('proposed', 'verified', 'rejected', 'disputed', 'superseded')");
                    table.CheckConstraint("ck_knowledge_methodological_framework_review_time", "reviewed_at IS NULL OR reviewed_at >= asserted_at");
                    table.CheckConstraint("ck_knowledge_methodological_framework_supersedes", "supersedes_assertion_id IS NULL OR supersedes_assertion_id <> id");
                    table.CheckConstraint("ck_knowledge_methodological_framework_type", "classification_type IN ('declared', 'analytical')");
                    table.ForeignKey(
                        name: "fk_method_framework_assertion_framework",
                        column: x => x.methodological_framework_id,
                        principalTable: "knowledge_methodological_frameworks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_method_framework_assertion_resource",
                        column: x => x.resource_id,
                        principalTable: "knowledge_resources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_method_framework_assertion_supersedes",
                        column: x => x.supersedes_assertion_id,
                        principalTable: "knowledge_methodological_framework_assertions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_method_framework_assertion_support_segment",
                        column: x => x.supporting_segment_id,
                        principalTable: "knowledge_document_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_segment_kind",
                table: "knowledge_document_segments",
                sql: "segment_kind IN ('unknown', 'main_text', 'pedagogical_prompt', 'sidebar', 'bibliography', 'caption', 'glossary', 'index')");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_epistemic_framework_assertions",
                table: "knowledge_epistemic_framework_assertions",
                columns: new[] { "resource_id", "epistemic_framework_id", "review_status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_epistemic_framework_assertions_epistemic_framewor~",
                table: "knowledge_epistemic_framework_assertions",
                column: "epistemic_framework_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_epistemic_framework_assertions_supersedes_asserti~",
                table: "knowledge_epistemic_framework_assertions",
                column: "supersedes_assertion_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_epistemic_framework_assertions_supporting_segment~",
                table: "knowledge_epistemic_framework_assertions",
                column: "supporting_segment_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_epistemic_frameworks_code",
                table: "knowledge_epistemic_frameworks",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_methodological_framework_assertions",
                table: "knowledge_methodological_framework_assertions",
                columns: new[] { "resource_id", "methodological_framework_id", "review_status" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_methodological_framework_assertions_methodologica~",
                table: "knowledge_methodological_framework_assertions",
                column: "methodological_framework_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_methodological_framework_assertions_supersedes_as~",
                table: "knowledge_methodological_framework_assertions",
                column: "supersedes_assertion_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_methodological_framework_assertions_supporting_se~",
                table: "knowledge_methodological_framework_assertions",
                column: "supporting_segment_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_methodological_frameworks_code",
                table: "knowledge_methodological_frameworks",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_epistemic_framework_assertions");

            migrationBuilder.DropTable(
                name: "knowledge_methodological_framework_assertions");

            migrationBuilder.DropTable(
                name: "knowledge_epistemic_frameworks");

            migrationBuilder.DropTable(
                name: "knowledge_methodological_frameworks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_segment_kind",
                table: "knowledge_document_segments");

            migrationBuilder.DropColumn(
                name: "segment_kind",
                table: "knowledge_document_segments");
        }
    }
}
