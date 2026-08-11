using Npgsql;

namespace ApologiaStudio.KnowledgeImporter;

internal static class KnowledgeLexicalSearch
{
    private const string Approved = "approved";

    public static async Task<KnowledgeLexicalSearchResponse> SearchAsync(
        string connectionString,
        string queryText,
        int topK,
        CancellationToken cancellationToken)
    {
        ValidateArguments(connectionString, queryText, topK);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var normalizedQuery = await BuildRelaxedQueryAsync(
            connection,
            transaction,
            queryText,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            await transaction.CommitAsync(cancellationToken);
            return new KnowledgeLexicalSearchResponse(null, []);
        }

        var sql = $"""
            WITH lexical_query AS (
                SELECT CAST(@tsquery AS tsquery) AS query
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
                ts_rank_cd(
                    to_tsvector('{DeDecretisLexicalSearchProfile.TextSearchConfiguration}', c.text),
                    q.query,
                    {DeDecretisLexicalSearchProfile.RankNormalization})::double precision AS score
            FROM knowledge_retrieval_chunks c
            CROSS JOIN lexical_query q
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
              AND a.sha256 = @artifact_sha256
              AND to_tsvector('{DeDecretisLexicalSearchProfile.TextSearchConfiguration}', c.text) @@ q.query
            ORDER BY score DESC, c.ordinal
            LIMIT @top_k
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("tsquery", normalizedQuery);
        command.Parameters.AddWithValue(
            "artifact_sha256",
            DeDecretisLexicalSearchProfile.NormalizedArtifactSha256);
        command.Parameters.AddWithValue("top_k", topK);

        var results = new List<KnowledgeLexicalSearchResult>(topK);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var result = new KnowledgeLexicalSearchResult(
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
                if (!double.IsFinite(result.Score) || result.Score < 0d)
                {
                    throw new KnowledgeImportException(
                        $"Lexical search returned an invalid score for chunk {result.ChunkId}.");
                }

                results.Add(result);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        var orderedResults = results
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.ChunkOrdinal)
            .ToArray();

        return new KnowledgeLexicalSearchResponse(normalizedQuery, orderedResults);
    }

    private static async Task<string?> BuildRelaxedQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string queryText,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"""
            SELECT string_agg(quote_literal(terms.term), ' | ' ORDER BY terms.term)
            FROM unnest(
                tsvector_to_array(
                    to_tsvector('{DeDecretisLexicalSearchProfile.TextSearchConfiguration}', @query_text)))
                AS terms(term)
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("query_text", queryText);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull)
        {
            return null;
        }

        return (string)value;
    }

    private static async Task EnsureSchemaAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                to_regclass('public.knowledge_retrieval_chunks') IS NOT NULL
                AND to_regclass('public.knowledge_chunk_segments') IS NOT NULL
                AND to_regclass('public.knowledge_document_segments') IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" LIKE '%_AddKnowledgeChunkEmbeddings')
            """,
            connection);

        var valid = await command.ExecuteScalarAsync(cancellationToken);
        if (valid is not true)
        {
            throw new KnowledgeImportException(
                "Knowledge lexical-search schema is not ready. Apply the 6E Knowledge migration first.");
        }
    }

    private static void ValidateArguments(
        string connectionString,
        string queryText,
        int topK)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);

        if (topK is < 1 or > DeDecretisLexicalSearchProfile.MaximumTopK)
        {
            throw new KnowledgeImportException(
                $"Top K must be between 1 and {DeDecretisLexicalSearchProfile.MaximumTopK}.");
        }
    }

    private static void ValidateEvidenceMapping(KnowledgeLexicalSearchResult result)
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
}
