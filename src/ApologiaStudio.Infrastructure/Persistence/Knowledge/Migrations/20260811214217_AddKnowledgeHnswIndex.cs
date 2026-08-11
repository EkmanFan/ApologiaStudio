using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeHnswIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(
                    """
                    CREATE INDEX ix_knowledge_chunk_embeddings_qwen3_4b_hnsw_cosine
                    ON knowledge_chunk_embeddings
                    USING hnsw ((embedding::halfvec(2560)) halfvec_cosine_ops)
                    WITH (m = 16, ef_construction = 64)
                    WHERE embedding_profile = 'de-decretis-retrieval-qwen3-embedding-4b-v1'
                      AND dimensions = 2560;
                    """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        migrationBuilder.Sql(
                    """
                    DROP INDEX ix_knowledge_chunk_embeddings_qwen3_4b_hnsw_cosine;
                    """);

        }
    }
}
