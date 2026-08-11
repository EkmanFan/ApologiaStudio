using System.Globalization;
using System.Text;
using Npgsql;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeVectorSearch
{
    private const string Approved = "approved";

    public static async Task<KnowledgeVectorSearchResponse> SearchAsync(
        string connectionString,
        IReadOnlyList<float> queryEmbedding,
        string modelDigest,
        int topK,
        VectorSearchMode mode,
        CancellationToken cancellationToken)
    {
        ValidateArguments(connectionString, queryEmbedding, modelDigest, topK);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var hnswIndexVerified = false;
        if (mode == VectorSearchMode.Hnsw)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                $"SET LOCAL hnsw.ef_search = {DeDecretisVectorSearchProfile.HnswEfSearch}",
                cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "SET LOCAL enable_seqscan = off",
                cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "SET LOCAL enable_bitmapscan = off",
                cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "SET LOCAL enable_sort = off",
                cancellationToken);
            hnswIndexVerified = await VerifyHnswPlanAsync(
                connection,
                transaction,
                queryEmbedding,
                modelDigest,
                topK,
                cancellationToken);
            if (!hnswIndexVerified)
            {
                throw new KnowledgeImportException(
                    $"PostgreSQL did not plan the HNSW query with index '{DeDecretisVectorSearchProfile.HnswIndexName}'.");
            }
        }

        var distanceExpression = mode == VectorSearchMode.Exact
            ? "e.embedding <=> CAST(@query_embedding AS vector)"
            : "(e.embedding::halfvec(2560)) <=> (CAST(@query_embedding AS vector)::halfvec(2560))";

        var sql = $"""
            WITH nearest AS MATERIALIZED (
                SELECT
                    e.chunk_id,
                    {distanceExpression} AS distance
                FROM knowledge_chunk_embeddings e
                WHERE e.embedding_profile = '{DeDecretisRetrievalProfile.ProfileId}'
                  AND e.provider = '{DeDecretisRetrievalProfile.EmbeddingProvider}'
                  AND e.model = '{DeDecretisRetrievalProfile.EmbeddingModel}'
                  AND e.model_digest = @model_digest
                  AND e.dimensions = {DeDecretisRetrievalProfile.EmbeddingDimensions}
                  AND EXISTS (
                      SELECT 1
                      FROM knowledge_retrieval_chunks eligible_chunk
                      JOIN knowledge_chunk_segments eligible_map
                        ON eligible_map.chunk_id = eligible_chunk.id
                       AND eligible_map.sequence = 0
                      JOIN knowledge_document_segments eligible_segment
                        ON eligible_segment.id = eligible_map.segment_id
                       AND eligible_segment.artifact_id = eligible_chunk.artifact_id
                      JOIN knowledge_artifacts eligible_artifact
                        ON eligible_artifact.id = eligible_chunk.artifact_id
                      JOIN knowledge_manifestations eligible_manifestation
                        ON eligible_manifestation.id = eligible_artifact.manifestation_id
                      JOIN knowledge_expressions eligible_expression
                        ON eligible_expression.id = eligible_manifestation.expression_id
                      JOIN knowledge_works eligible_work
                        ON eligible_work.id = eligible_expression.work_id
                      JOIN knowledge_resources eligible_segment_review
                        ON eligible_segment_review.id = eligible_segment.id
                       AND eligible_segment_review.editorial_review_status = '{Approved}'
                      JOIN knowledge_resources eligible_artifact_review
                        ON eligible_artifact_review.id = eligible_artifact.id
                       AND eligible_artifact_review.editorial_review_status = '{Approved}'
                      JOIN knowledge_resources eligible_manifestation_review
                        ON eligible_manifestation_review.id = eligible_manifestation.id
                       AND eligible_manifestation_review.editorial_review_status = '{Approved}'
                      JOIN knowledge_resources eligible_expression_review
                        ON eligible_expression_review.id = eligible_expression.id
                       AND eligible_expression_review.editorial_review_status = '{Approved}'
                      JOIN knowledge_resources eligible_work_review
                        ON eligible_work_review.id = eligible_work.id
                       AND eligible_work_review.editorial_review_status = '{Approved}'
                      WHERE eligible_chunk.id = e.chunk_id
                        AND eligible_chunk.chunking_strategy = '{DeDecretisRetrievalProfile.ChunkingStrategy}'
                        AND eligible_chunk.chunking_version = '{DeDecretisRetrievalProfile.ChunkingVersion}'
                  )
                ORDER BY {distanceExpression}
                LIMIT @top_k
            )
            SELECT
                c.id,
                c.ordinal,
                c.text,
                s.id,
                s.ordinal,
                s.locator,
                s.title,
                s.text,
                cs.start_offset,
                cs.end_offset,
                w.title,
                m.citation_label,
                nearest.distance
            FROM nearest
            JOIN knowledge_retrieval_chunks c
              ON c.id = nearest.chunk_id
            JOIN knowledge_chunk_segments cs
              ON cs.chunk_id = c.id
             AND cs.sequence = 0
            JOIN knowledge_document_segments s
              ON s.id = cs.segment_id
             AND s.artifact_id = c.artifact_id
            JOIN knowledge_artifacts a
              ON a.id = c.artifact_id
            JOIN knowledge_manifestations m
              ON m.id = a.manifestation_id
            JOIN knowledge_expressions x
              ON x.id = m.expression_id
            JOIN knowledge_works w
              ON w.id = x.work_id
            JOIN knowledge_resources segment_review
              ON segment_review.id = s.id
             AND segment_review.editorial_review_status = '{Approved}'
            JOIN knowledge_resources artifact_review
              ON artifact_review.id = a.id
             AND artifact_review.editorial_review_status = '{Approved}'
            JOIN knowledge_resources manifestation_review
              ON manifestation_review.id = m.id
             AND manifestation_review.editorial_review_status = '{Approved}'
            JOIN knowledge_resources expression_review
              ON expression_review.id = x.id
             AND expression_review.editorial_review_status = '{Approved}'
            JOIN knowledge_resources work_review
              ON work_review.id = w.id
             AND work_review.editorial_review_status = '{Approved}'
            WHERE c.chunking_strategy = '{DeDecretisRetrievalProfile.ChunkingStrategy}'
              AND c.chunking_version = '{DeDecretisRetrievalProfile.ChunkingVersion}'
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("query_embedding", ToVectorLiteral(queryEmbedding));
        command.Parameters.AddWithValue("model_digest", modelDigest);
        command.Parameters.AddWithValue("top_k", topK);

        var results = new List<KnowledgeVectorSearchResult>(topK);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var result = new KnowledgeVectorSearchResult(
                    reader.GetGuid(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetGuid(3),
                    reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7),
                    reader.GetInt32(8),
                    reader.GetInt32(9),
                    reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.GetDouble(12));

                ValidateEvidenceMapping(result);
                if (!double.IsFinite(result.Distance))
                {
                    throw new KnowledgeImportException(
                        $"Vector search returned a non-finite distance for chunk {result.ChunkId}.");
                }

                results.Add(result);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        if (results.Count == 0)
        {
            throw new KnowledgeImportException(
                "Vector search returned no approved retrieval chunks for the pinned embedding profile and model digest.");
        }

        var orderedResults = results
            .OrderBy(result => result.Distance)
            .ThenBy(result => result.ChunkOrdinal)
            .ToArray();

        return new KnowledgeVectorSearchResponse(mode, hnswIndexVerified, orderedResults);
    }

    private static async Task<bool> VerifyHnswPlanAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<float> queryEmbedding,
        string modelDigest,
        int topK,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            EXPLAIN (FORMAT TEXT)
            SELECT e.chunk_id
            FROM knowledge_chunk_embeddings e
            WHERE e.embedding_profile = '{DeDecretisRetrievalProfile.ProfileId}'
              AND e.model_digest = @model_digest
              AND e.dimensions = {DeDecretisRetrievalProfile.EmbeddingDimensions}
            ORDER BY (e.embedding::halfvec(2560)) <=> (CAST(@query_embedding AS vector)::halfvec(2560))
            LIMIT @top_k
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("query_embedding", ToVectorLiteral(queryEmbedding));
        command.Parameters.AddWithValue("model_digest", modelDigest);
        command.Parameters.AddWithValue("top_k", topK);

        var plan = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            plan.AppendLine(reader.GetString(0));
        }

        return plan.ToString().Contains(
            DeDecretisVectorSearchProfile.HnswIndexName,
            StringComparison.Ordinal);
    }

    private static async Task EnsureSchemaAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"""
            SELECT
                to_regclass('public.knowledge_retrieval_chunks') IS NOT NULL
                AND to_regclass('public.knowledge_chunk_segments') IS NOT NULL
                AND to_regclass('public.knowledge_chunk_embeddings') IS NOT NULL
                AND to_regclass('public.{DeDecretisVectorSearchProfile.HnswIndexName}') IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" LIKE '%_AddKnowledgeHnswIndex')
            """,
            connection);

        var valid = await command.ExecuteScalarAsync(cancellationToken);
        if (valid is not true)
        {
            throw new KnowledgeImportException(
                "Knowledge vector-search schema is not ready. Apply the 6F Knowledge migration first.");
        }
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateArguments(
        string connectionString,
        IReadOnlyList<float> queryEmbedding,
        string modelDigest,
        int topK)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(queryEmbedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDigest);

        if (queryEmbedding.Count != DeDecretisRetrievalProfile.EmbeddingDimensions ||
            queryEmbedding.Any(value => !float.IsFinite(value)) ||
            queryEmbedding.All(value => value == 0f))
        {
            throw new KnowledgeImportException(
                $"Query embedding must contain {DeDecretisRetrievalProfile.EmbeddingDimensions} finite, non-zero dimensions.");
        }

        if (!IsSha256(modelDigest))
        {
            throw new KnowledgeImportException(
                "The Ollama model digest must be a lowercase SHA-256 value.");
        }

        if (topK is < 1 or > DeDecretisVectorSearchProfile.MaximumTopK)
        {
            throw new KnowledgeImportException(
                $"Top K must be between 1 and {DeDecretisVectorSearchProfile.MaximumTopK}.");
        }
    }

    private static void ValidateEvidenceMapping(KnowledgeVectorSearchResult result)
    {
        if (result.StartOffset < 0 ||
            result.EndOffset <= result.StartOffset ||
            result.EndOffset > result.SegmentText.Length)
        {
            throw new KnowledgeImportException(
                $"Chunk {result.ChunkId} has invalid offsets for segment {result.SegmentId}.");
        }

        var mappedText = result.SegmentText[result.StartOffset..result.EndOffset];
        if (!string.Equals(mappedText, result.ChunkText, StringComparison.Ordinal))
        {
            throw new KnowledgeImportException(
                $"Chunk {result.ChunkId} no longer matches its citable source segment {result.SegmentId}.");
        }
    }

    private static string ToVectorLiteral(IReadOnlyList<float> vector)
    {
        return "[" + string.Join(
            ",",
            vector.Select(value => value.ToString("R", CultureInfo.InvariantCulture))) + "]";
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
