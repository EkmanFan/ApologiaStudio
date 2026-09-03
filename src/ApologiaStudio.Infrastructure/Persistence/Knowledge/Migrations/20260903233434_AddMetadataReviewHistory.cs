using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataReviewHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "metadata_review_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    policy_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model_provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    model_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    insufficient_evidence = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_milliseconds = table.Column<double>(type: "double precision", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    superseded_by_analysis_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metadata_review_analyses", x => x.id);
                    table.CheckConstraint("ck_metadata_review_analysis_failure", "(status = 'failed') = (failure_reason IS NOT NULL)");
                    table.CheckConstraint("ck_metadata_review_analysis_outcome", "reviewer_outcome IS NULL OR reviewer_outcome IN ('accepted', 'modified', 'rejected')");
                    table.CheckConstraint("ck_metadata_review_analysis_status", "status IN ('valid', 'failed')");
                    table.CheckConstraint("ck_metadata_review_analysis_supersedes", "superseded_by_analysis_id IS NULL OR superseded_by_analysis_id <> id");
                    table.ForeignKey(
                        name: "FK_metadata_review_analyses_document_manager_editorial_drafts_~",
                        column: x => x.draft_id,
                        principalTable: "document_manager_editorial_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_metadata_review_analyses_metadata_review_analyses_supersede~",
                        column: x => x.superseded_by_analysis_id,
                        principalTable: "metadata_review_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "metadata_review_suggestions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disposition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    justification = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metadata_review_suggestions", x => x.id);
                    table.CheckConstraint("ck_metadata_review_suggestion_disposition", "disposition IN ('suggested', 'considered_but_rejected')");
                    table.ForeignKey(
                        name: "FK_metadata_review_suggestions_genre_form_authority_terms_term~",
                        column: x => x.term_id,
                        principalTable: "genre_form_authority_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_metadata_review_suggestions_metadata_review_analyses_analys~",
                        column: x => x.analysis_id,
                        principalTable: "metadata_review_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metadata_review_suggestion_evidence",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    suggestion_id = table.Column<long>(type: "bigint", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metadata_review_suggestion_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_metadata_review_suggestion_evidence_metadata_review_suggest~",
                        column: x => x.suggestion_id,
                        principalTable: "metadata_review_suggestions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_metadata_review_analyses_current",
                table: "metadata_review_analyses",
                columns: new[] { "draft_id", "field", "superseded_by_analysis_id" });

            migrationBuilder.CreateIndex(
                name: "ix_metadata_review_analyses_history",
                table: "metadata_review_analyses",
                columns: new[] { "draft_id", "completed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_metadata_review_analyses_superseded_by_analysis_id",
                table: "metadata_review_analyses",
                column: "superseded_by_analysis_id");

            migrationBuilder.CreateIndex(
                name: "ux_metadata_review_suggestion_evidence",
                table: "metadata_review_suggestion_evidence",
                columns: new[] { "suggestion_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metadata_review_suggestions_term_id",
                table: "metadata_review_suggestions",
                column: "term_id");

            migrationBuilder.CreateIndex(
                name: "ux_metadata_review_suggestions",
                table: "metadata_review_suggestions",
                columns: new[] { "analysis_id", "term_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metadata_review_suggestion_evidence");

            migrationBuilder.DropTable(
                name: "metadata_review_suggestions");

            migrationBuilder.DropTable(
                name: "metadata_review_analyses");
        }
    }
}
