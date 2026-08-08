using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseBackedAiRuntimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_runtime_settings",
                columns: table => new
                {
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    base_address = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    routing_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    default_agent_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    routing_timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    generation_timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    keep_alive = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    maximum_history_messages = table.Column<int>(type: "integer", nullable: false),
                    maximum_history_characters = table.Column<int>(type: "integer", nullable: false),
                    maximum_output_tokens = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_runtime_settings", x => x.provider);
                    table.CheckConstraint("ck_ai_runtime_settings_generation_timeout", "generation_timeout_seconds BETWEEN 1 AND 600");
                    table.CheckConstraint("ck_ai_runtime_settings_history_characters", "maximum_history_characters BETWEEN 1000 AND 100000");
                    table.CheckConstraint("ck_ai_runtime_settings_history_messages", "maximum_history_messages BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_ai_runtime_settings_output_tokens", "maximum_output_tokens BETWEEN 64 AND 8192");
                    table.CheckConstraint("ck_ai_runtime_settings_provider", "provider = 'Ollama'");
                    table.CheckConstraint("ck_ai_runtime_settings_routing_timeout", "routing_timeout_seconds BETWEEN 1 AND 300");
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_model_assignments",
                columns: table => new
                {
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_model_assignments", x => new { x.provider, x.agent_id });
                    table.ForeignKey(
                        name: "FK_ai_agent_model_assignments_ai_runtime_settings_provider",
                        column: x => x.provider,
                        principalTable: "ai_runtime_settings",
                        principalColumn: "provider",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_agent_model_assignments_agent_id",
                table: "ai_agent_model_assignments",
                column: "agent_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_model_assignments");

            migrationBuilder.DropTable(
                name: "ai_runtime_settings");
        }
    }
}
