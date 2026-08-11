using System.Globalization;
using Npgsql;

namespace ApologiaStudio.KnowledgeImporter;

internal sealed record KnowledgeRetrievalProjectionResult(
    bool WasCreated,
    Guid NormalizedArtifactId,
    int ChunkCount,
    int EmbeddingCount,
    string ModelDigest);

internal static class KnowledgeRetrievalProjectionWriter
{
    private static readonly Guid NormalizedArtifactId =
        StableKnowledgeIds.ForProfile("normalized-artifact");

    public static async Task<bool> ExistsAndMatchesAsync(
        string connectionString,
        PreparedDeDecretis prepared,
        IReadOnlyList<PreparedRetrievalChunk> chunks,
        string modelDigest,
        CancellationToken cancellationToken)
    {
        ValidateArguments(connectionString, prepared, chunks, modelDigest, embeddings: null);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AcquireProjectionLockAsync(connection, transaction, cancellationToken);
        await ValidateSourceAsync(connection, transaction, prepared, cancellationToken);

        var existingCount = await CountProjectionChunksAsync(
            connection,
            transaction,
            cancellationToken);
        if (existingCount == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        await ValidateExistingProjectionAsync(
            connection,
            transaction,
            chunks,
            modelDigest,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public static async Task<KnowledgeRetrievalProjectionResult> ProjectAsync(
        string connectionString,
        PreparedDeDecretis prepared,
        IReadOnlyList<PreparedRetrievalChunk> chunks,
        string modelDigest,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken)
    {
        ValidateArguments(connectionString, prepared, chunks, modelDigest, embeddings);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AcquireProjectionLockAsync(connection, transaction, cancellationToken);
        await ValidateSourceAsync(connection, transaction, prepared, cancellationToken);

        var existingCount = await CountProjectionChunksAsync(
            connection,
            transaction,
            cancellationToken);
        if (existingCount != 0)
        {
            await ValidateExistingProjectionAsync(
                connection,
                transaction,
                chunks,
                modelDigest,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new KnowledgeRetrievalProjectionResult(
                false,
                NormalizedArtifactId,
                chunks.Count,
                chunks.Count,
                modelDigest);
        }

        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < chunks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = chunks[index];
            var embeddingId = GetEmbeddingId(chunk.Id);

            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO knowledge_retrieval_chunks
                    (id, artifact_id, ordinal, text, chunking_strategy, chunking_version, created_at)
                VALUES
                    (@id, @artifact_id, @ordinal, @text, @strategy, @version, @created_at)
                """,
                cancellationToken,
                ("id", chunk.Id),
                ("artifact_id", NormalizedArtifactId),
                ("ordinal", chunk.Ordinal),
                ("text", chunk.Text),
                ("strategy", DeDecretisRetrievalProfile.ChunkingStrategy),
                ("version", DeDecretisRetrievalProfile.ChunkingVersion),
                ("created_at", now));

            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO knowledge_chunk_segments
                    (chunk_id, segment_id, sequence, start_offset, end_offset)
                VALUES
                    (@chunk_id, @segment_id, 0, @start_offset, @end_offset)
                """,
                cancellationToken,
                ("chunk_id", chunk.Id),
                ("segment_id", chunk.SegmentId),
                ("start_offset", chunk.StartOffset),
                ("end_offset", chunk.EndOffset));

            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO knowledge_chunk_embeddings
                    (id, chunk_id, embedding_profile, provider, model, model_digest,
                     dimensions, embedding, created_at)
                VALUES
                    (@id, @chunk_id, @profile, @provider, @model, @model_digest,
                     @dimensions, CAST(@embedding AS vector), @created_at)
                """,
                cancellationToken,
                ("id", embeddingId),
                ("chunk_id", chunk.Id),
                ("profile", DeDecretisRetrievalProfile.ProfileId),
                ("provider", DeDecretisRetrievalProfile.EmbeddingProvider),
                ("model", DeDecretisRetrievalProfile.EmbeddingModel),
                ("model_digest", modelDigest),
                ("dimensions", DeDecretisRetrievalProfile.EmbeddingDimensions),
                ("embedding", ToVectorLiteral(embeddings[index])),
                ("created_at", now));
        }

        await ValidateExistingProjectionAsync(
            connection,
            transaction,
            chunks,
            modelDigest,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new KnowledgeRetrievalProjectionResult(
            true,
            NormalizedArtifactId,
            chunks.Count,
            chunks.Count,
            modelDigest);
    }

    private static void ValidateArguments(
        string connectionString,
        PreparedDeDecretis prepared,
        IReadOnlyList<PreparedRetrievalChunk> chunks,
        string modelDigest,
        IReadOnlyList<float[]>? embeddings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDigest);

        if (chunks.Count == 0)
        {
            throw new KnowledgeImportException(
                "The retrieval projection contains no chunks.");
        }

        if (!IsSha256(modelDigest))
        {
            throw new KnowledgeImportException(
                "The Ollama model digest must be a lowercase SHA-256 value.");
        }

        if (embeddings is null)
        {
            return;
        }

        if (embeddings.Count != chunks.Count)
        {
            throw new KnowledgeImportException(
                $"Embedding count {embeddings.Count} does not match chunk count {chunks.Count}.");
        }

        for (var index = 0; index < embeddings.Count; index++)
        {
            var embedding = embeddings[index];
            if (embedding.Length != DeDecretisRetrievalProfile.EmbeddingDimensions ||
                embedding.Any(value => !float.IsFinite(value)))
            {
                throw new KnowledgeImportException(
                    $"Embedding {index} is invalid for the configured retrieval profile.");
            }
        }
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
                AND to_regclass('public.knowledge_chunk_embeddings') IS NOT NULL
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
                "Knowledge retrieval schema is not ready. Apply the 6E Knowledge migration first.");
        }
    }

    private static async Task AcquireProjectionLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext(@key))",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", DeDecretisRetrievalProfile.ProfileId);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ValidateSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PreparedDeDecretis prepared,
        CancellationToken cancellationToken)
    {
        await using (var artifactCommand = new NpgsqlCommand(
                         """
                         SELECT a.sha256, a.lifecycle_status, r.editorial_review_status
                         FROM knowledge_artifacts a
                         JOIN knowledge_resources r ON r.id = a.id
                         WHERE a.id = @artifact_id
                         """,
                         connection,
                         transaction))
        {
            artifactCommand.Parameters.AddWithValue("artifact_id", NormalizedArtifactId);
            await using var reader = await artifactCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                !string.Equals(reader.GetString(0).Trim(), prepared.NormalizedArtifact.Sha256, StringComparison.Ordinal) ||
                !string.Equals(reader.GetString(1), "active", StringComparison.Ordinal) ||
                !string.Equals(reader.GetString(2), "approved", StringComparison.Ordinal) ||
                await reader.ReadAsync(cancellationToken))
            {
                throw new KnowledgeImportException(
                    "The approved normalized De Decretis artifact is missing or does not match the curated source.");
            }
        }

        await using var segmentCommand = new NpgsqlCommand(
            """
            SELECT s.id, s.ordinal, s.text, s.locator, r.editorial_review_status
            FROM knowledge_document_segments s
            JOIN knowledge_resources r ON r.id = s.id
            WHERE s.artifact_id = @artifact_id
            ORDER BY s.ordinal
            """,
            connection,
            transaction);
        segmentCommand.Parameters.AddWithValue("artifact_id", NormalizedArtifactId);

        await using var segmentReader = await segmentCommand.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await segmentReader.ReadAsync(cancellationToken))
        {
            if (index >= prepared.Segments.Count)
            {
                throw new KnowledgeImportException(
                    "The persisted De Decretis source has more segments than the curated profile.");
            }

            var expected = prepared.Segments[index++];
            if (segmentReader.GetGuid(0) != expected.Id ||
                segmentReader.GetInt32(1) != expected.Number ||
                !string.Equals(segmentReader.GetString(2), expected.Text, StringComparison.Ordinal) ||
                !string.Equals(segmentReader.GetString(3), expected.Locator, StringComparison.Ordinal) ||
                !string.Equals(segmentReader.GetString(4), "approved", StringComparison.Ordinal))
            {
                throw new KnowledgeImportException(
                    $"Persisted section §{expected.Number} does not match the approved curated profile.");
            }
        }

        if (index != prepared.Segments.Count)
        {
            throw new KnowledgeImportException(
                $"The persisted De Decretis source has {index} approved segments; expected {prepared.Segments.Count}.");
        }
    }

    private static async Task<int> CountProjectionChunksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM knowledge_retrieval_chunks
            WHERE artifact_id = @artifact_id
              AND chunking_strategy = @strategy
              AND chunking_version = @version
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("artifact_id", NormalizedArtifactId);
        command.Parameters.AddWithValue("strategy", DeDecretisRetrievalProfile.ChunkingStrategy);
        command.Parameters.AddWithValue("version", DeDecretisRetrievalProfile.ChunkingVersion);
        return checked((int)(long)(await command.ExecuteScalarAsync(cancellationToken))!);
    }

    private static async Task ValidateExistingProjectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<PreparedRetrievalChunk> chunks,
        string modelDigest,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                c.id,
                c.ordinal,
                c.text,
                cs.segment_id,
                cs.sequence,
                cs.start_offset,
                cs.end_offset,
                e.id,
                e.provider,
                e.model,
                e.model_digest,
                e.dimensions,
                vector_dims(e.embedding)
            FROM knowledge_retrieval_chunks c
            JOIN knowledge_chunk_segments cs ON cs.chunk_id = c.id
            JOIN knowledge_chunk_embeddings e
              ON e.chunk_id = c.id
             AND e.embedding_profile = @profile
            WHERE c.artifact_id = @artifact_id
              AND c.chunking_strategy = @strategy
              AND c.chunking_version = @version
            ORDER BY c.ordinal, cs.sequence
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("profile", DeDecretisRetrievalProfile.ProfileId);
        command.Parameters.AddWithValue("artifact_id", NormalizedArtifactId);
        command.Parameters.AddWithValue("strategy", DeDecretisRetrievalProfile.ChunkingStrategy);
        command.Parameters.AddWithValue("version", DeDecretisRetrievalProfile.ChunkingVersion);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (index >= chunks.Count)
            {
                throw new KnowledgeImportException(
                    "The existing retrieval projection contains unexpected extra rows.");
            }

            var expected = chunks[index++];
            var persistedDigest = reader.GetString(10).Trim();

            if (reader.GetGuid(0) != expected.Id ||
                reader.GetInt32(1) != expected.Ordinal ||
                !string.Equals(reader.GetString(2), expected.Text, StringComparison.Ordinal) ||
                reader.GetGuid(3) != expected.SegmentId ||
                reader.GetInt32(4) != 0 ||
                reader.GetInt32(5) != expected.StartOffset ||
                reader.GetInt32(6) != expected.EndOffset ||
                reader.GetGuid(7) != GetEmbeddingId(expected.Id) ||
                !string.Equals(reader.GetString(8), DeDecretisRetrievalProfile.EmbeddingProvider, StringComparison.Ordinal) ||
                !string.Equals(reader.GetString(9), DeDecretisRetrievalProfile.EmbeddingModel, StringComparison.Ordinal) ||
                !string.Equals(persistedDigest, modelDigest, StringComparison.Ordinal) ||
                reader.GetInt32(11) != DeDecretisRetrievalProfile.EmbeddingDimensions ||
                reader.GetInt32(12) != DeDecretisRetrievalProfile.EmbeddingDimensions)
            {
                throw new KnowledgeImportException(
                    $"Existing retrieval projection row {expected.Ordinal} does not match profile {DeDecretisRetrievalProfile.ProfileId}.");
            }
        }

        if (index != chunks.Count)
        {
            throw new KnowledgeImportException(
                $"Existing retrieval projection has {index} complete chunk/embedding rows; expected {chunks.Count}.");
        }
    }

    private static Guid GetEmbeddingId(Guid chunkId) =>
        StableKnowledgeIds.ForProfile(
            $"retrieval/{DeDecretisRetrievalProfile.ProfileId}/embedding/{chunkId:D}");

    private static string ToVectorLiteral(IReadOnlyList<float> values) =>
        "[" + string.Join(
            ",",
            values.Select(value => value.ToString("R", CultureInfo.InvariantCulture))) + "]";

    private static bool IsSha256(string value) =>
        value.Length == 64 &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
