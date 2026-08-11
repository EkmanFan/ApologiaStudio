using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeChunkEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_chunk_embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding_profile = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    model_digest = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    dimensions = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunk_embeddings", x => x.id);
                    table.CheckConstraint("ck_knowledge_chunk_embedding_digest", "model_digest ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_knowledge_chunk_embedding_dimensions", "dimensions BETWEEN 1 AND 16000 AND vector_dims(embedding) = dimensions");
                    table.ForeignKey(
                        name: "FK_knowledge_chunk_embeddings_knowledge_retrieval_chunks_chunk~",
                        column: x => x.chunk_id,
                        principalTable: "knowledge_retrieval_chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_chunk_embeddings_model",
                table: "knowledge_chunk_embeddings",
                columns: new[] { "embedding_profile", "model_digest" });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_chunk_embeddings_profile",
                table: "knowledge_chunk_embeddings",
                columns: new[] { "chunk_id", "embedding_profile" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_chunk_embeddings");
        }
    }
}
