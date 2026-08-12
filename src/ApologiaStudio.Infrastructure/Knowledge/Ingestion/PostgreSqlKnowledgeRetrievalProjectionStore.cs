using System.Globalization;
using ApologiaStudio.Application.Knowledge.Ingestion;
using Npgsql;

namespace ApologiaStudio.Infrastructure.Knowledge.Ingestion;

public static class PostgreSqlKnowledgeRetrievalProjectionStore
{
    public static async Task<bool> ExistsAndMatchesAsync(
        string connectionString,
        KnowledgeImportPackage package,
        KnowledgeRetrievalProfile profile,
        IReadOnlyList<KnowledgeRetrievalChunk> chunks,
        string modelDigest,
        CancellationToken cancellationToken)
    {
        ValidateArguments(
            connectionString,
            package,
            profile,
            chunks,
            modelDigest,
            embeddings: null);

        await using var connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync(
            cancellationToken);

        await EnsureSchemaAsync(
            connection,
            cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(
                cancellationToken);

        await AcquireProjectionLockAsync(
            connection,
            transaction,
            package,
            profile,
            cancellationToken);

        await ValidateSourceAsync(
            connection,
            transaction,
            package,
            cancellationToken);

        var existingCount =
            await CountProjectionChunksAsync(
                connection,
                transaction,
                package.NormalizedArtifactId,
                profile,
                cancellationToken);

        if (existingCount == 0)
        {
            await transaction.CommitAsync(
                cancellationToken);

            return false;
        }

        await ValidateExistingProjectionAsync(
            connection,
            transaction,
            package,
            profile,
            chunks,
            modelDigest,
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return true;
    }

    public static async Task<
        KnowledgeRetrievalProjectionResult>
        ProjectAsync(
            string connectionString,
            KnowledgeImportPackage package,
            KnowledgeRetrievalProfile profile,
            IReadOnlyList<KnowledgeRetrievalChunk> chunks,
            string modelDigest,
            IReadOnlyList<float[]> embeddings,
            CancellationToken cancellationToken)
    {
        ValidateArguments(
            connectionString,
            package,
            profile,
            chunks,
            modelDigest,
            embeddings);

        await using var connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync(
            cancellationToken);

        await EnsureSchemaAsync(
            connection,
            cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(
                cancellationToken);

        await AcquireProjectionLockAsync(
            connection,
            transaction,
            package,
            profile,
            cancellationToken);

        await ValidateSourceAsync(
            connection,
            transaction,
            package,
            cancellationToken);

        var existingCount =
            await CountProjectionChunksAsync(
                connection,
                transaction,
                package.NormalizedArtifactId,
                profile,
                cancellationToken);

        if (existingCount != 0)
        {
            await ValidateExistingProjectionAsync(
                connection,
                transaction,
                package,
                profile,
                chunks,
                modelDigest,
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return new KnowledgeRetrievalProjectionResult(
                false,
                package.NormalizedArtifactId,
                chunks.Count,
                chunks.Count,
                modelDigest);
        }

        var now = DateTimeOffset.UtcNow;

        for (var index = 0;
             index < chunks.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = chunks[index];
            var embeddingId = GetEmbeddingId(
                package,
                profile,
                chunk.Id);

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
                ("artifact_id", package.NormalizedArtifactId),
                ("ordinal", chunk.Ordinal),
                ("text", chunk.Text),
                ("strategy", profile.ChunkingStrategy),
                ("version", profile.ChunkingVersion),
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
                ("profile", profile.ProfileId),
                ("provider", profile.EmbeddingProvider),
                ("model", profile.EmbeddingModel),
                ("model_digest", modelDigest),
                ("dimensions", profile.EmbeddingDimensions),
                ("embedding", ToVectorLiteral(
                    embeddings[index])),
                ("created_at", now));
        }

        await ValidateExistingProjectionAsync(
            connection,
            transaction,
            package,
            profile,
            chunks,
            modelDigest,
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return new KnowledgeRetrievalProjectionResult(
            true,
            package.NormalizedArtifactId,
            chunks.Count,
            chunks.Count,
            modelDigest);
    }

    private static void ValidateArguments(
        string connectionString,
        KnowledgeImportPackage package,
        KnowledgeRetrievalProfile profile,
        IReadOnlyList<KnowledgeRetrievalChunk> chunks,
        string modelDigest,
        IReadOnlyList<float[]>? embeddings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);
        KnowledgeImportPackageValidator.Validate(package);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            modelDigest);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException(
                "The retrieval projection contains no chunks.");
        }

        if (!IsSha256(modelDigest))
        {
            throw new InvalidOperationException(
                "The embedding model digest must be a lowercase SHA-256 value.");
        }

        if (profile.EmbeddingDimensions <= 0)
        {
            throw new InvalidOperationException(
                $"Retrieval profile {profile.ProfileId} has invalid embedding dimensions.");
        }

        if (embeddings is null)
        {
            return;
        }

        if (embeddings.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Embedding count {embeddings.Count} does not match chunk count {chunks.Count}.");
        }

        for (var index = 0;
             index < embeddings.Count;
             index++)
        {
            var embedding = embeddings[index];

            if (embedding.Length !=
                    profile.EmbeddingDimensions ||
                embedding.Any(value =>
                    !float.IsFinite(value)))
            {
                throw new InvalidOperationException(
                    $"Embedding {index} is invalid for retrieval profile {profile.ProfileId}.");
            }
        }
    }

    private static async Task EnsureSchemaAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
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

        var valid =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (valid is not true)
        {
            throw new InvalidOperationException(
                "Knowledge retrieval schema is not ready. Apply the Knowledge retrieval migration first.");
        }
    }

    private static async Task AcquireProjectionLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportPackage package,
        KnowledgeRetrievalProfile profile,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtext(@key))",
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "key",
            package.ProfileId +
            "/retrieval/" +
            profile.ProfileId);

        await command.ExecuteScalarAsync(
            cancellationToken);
    }

    private static async Task ValidateSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportPackage package,
        CancellationToken cancellationToken)
    {
        var normalizedArtifact =
            package.Artifacts.Single(
                artifact =>
                    artifact.Id ==
                    package.NormalizedArtifactId);

        if (!string.Equals(
                normalizedArtifact.LifecycleStatus,
                "active",
                StringComparison.Ordinal) ||
            !string.Equals(
                normalizedArtifact.EditorialReviewStatus,
                "approved",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Retrieval projection requires an active, editorially approved normalized artifact.");
        }

        await using (var artifactCommand =
                     new NpgsqlCommand(
                         """
                         SELECT
                             a.sha256,
                             a.lifecycle_status,
                             r.editorial_review_status
                         FROM knowledge_artifacts a
                         JOIN knowledge_resources r
                           ON r.id = a.id
                         WHERE a.id = @artifact_id
                         """,
                         connection,
                         transaction))
        {
            artifactCommand.Parameters.AddWithValue(
                "artifact_id",
                package.NormalizedArtifactId);

            await using var reader =
                await artifactCommand.ExecuteReaderAsync(
                    cancellationToken);

            if (!await reader.ReadAsync(
                    cancellationToken) ||
                !string.Equals(
                    reader.GetString(0).Trim(),
                    normalizedArtifact.Sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    reader.GetString(1),
                    normalizedArtifact.LifecycleStatus,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    reader.GetString(2),
                    normalizedArtifact.EditorialReviewStatus,
                    StringComparison.Ordinal) ||
                await reader.ReadAsync(
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    $"The normalized artifact for profile {package.ProfileId} is missing or does not match the import package.");
            }
        }

        var expectedSegments =
            package.Segments
                .Where(segment =>
                    segment.ArtifactId ==
                    package.NormalizedArtifactId)
                .OrderBy(segment =>
                    segment.Ordinal)
                .ToArray();

        await using var segmentCommand =
            new NpgsqlCommand(
                """
                SELECT
                    s.id,
                    s.ordinal,
                    s.segment_type,
                    s.segment_kind,
                    s.text,
                    s.locator,
                    r.editorial_review_status
                FROM knowledge_document_segments s
                JOIN knowledge_resources r
                  ON r.id = s.id
                WHERE s.artifact_id = @artifact_id
                ORDER BY s.ordinal
                """,
                connection,
                transaction);

        segmentCommand.Parameters.AddWithValue(
            "artifact_id",
            package.NormalizedArtifactId);

        await using var segmentReader =
            await segmentCommand.ExecuteReaderAsync(
                cancellationToken);

        var index = 0;

        while (await segmentReader.ReadAsync(
                   cancellationToken))
        {
            if (index >= expectedSegments.Length)
            {
                throw new InvalidOperationException(
                    $"The persisted source for profile {package.ProfileId} has more segments than the import package.");
            }

            var expected =
                expectedSegments[index++];

            if (segmentReader.GetGuid(0) !=
                    expected.Id ||
                segmentReader.GetInt32(1) !=
                    expected.Ordinal ||
                !string.Equals(
                    segmentReader.GetString(2),
                    PostgreSqlKnowledgeImportStore.ToDatabaseValue(
                        expected.SegmentType),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    segmentReader.GetString(3),
                    PostgreSqlKnowledgeImportStore.ToDatabaseValue(
                        expected.SegmentKind),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    segmentReader.GetString(4),
                    expected.Text,
                    StringComparison.Ordinal) ||
                !NullableStringEquals(
                    segmentReader,
                    5,
                    expected.Locator) ||
                !string.Equals(
                    segmentReader.GetString(6),
                    expected.EditorialReviewStatus,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Persisted segment {expected.Ordinal} does not match import profile {package.ProfileId}.");
            }
        }

        if (index != expectedSegments.Length)
        {
            throw new InvalidOperationException(
                $"The persisted source for profile {package.ProfileId} has {index} segments; expected {expectedSegments.Length}.");
        }
    }

    private static async Task<int>
        CountProjectionChunksAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid normalizedArtifactId,
            KnowledgeRetrievalProfile profile,
            CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM knowledge_retrieval_chunks
                WHERE artifact_id = @artifact_id
                  AND chunking_strategy = @strategy
                  AND chunking_version = @version
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "artifact_id",
            normalizedArtifactId);
        command.Parameters.AddWithValue(
            "strategy",
            profile.ChunkingStrategy);
        command.Parameters.AddWithValue(
            "version",
            profile.ChunkingVersion);

        return checked(
            (int)(long)(
                await command.ExecuteScalarAsync(
                    cancellationToken))!);
    }

    private static async Task
        ValidateExistingProjectionAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            KnowledgeImportPackage package,
            KnowledgeRetrievalProfile profile,
            IReadOnlyList<KnowledgeRetrievalChunk> chunks,
            string modelDigest,
            CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
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
                JOIN knowledge_chunk_segments cs
                  ON cs.chunk_id = c.id
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

        command.Parameters.AddWithValue(
            "profile",
            profile.ProfileId);
        command.Parameters.AddWithValue(
            "artifact_id",
            package.NormalizedArtifactId);
        command.Parameters.AddWithValue(
            "strategy",
            profile.ChunkingStrategy);
        command.Parameters.AddWithValue(
            "version",
            profile.ChunkingVersion);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        var index = 0;

        while (await reader.ReadAsync(
                   cancellationToken))
        {
            if (index >= chunks.Count)
            {
                throw new InvalidOperationException(
                    "The existing retrieval projection contains unexpected extra rows.");
            }

            var expected = chunks[index++];
            var persistedDigest =
                reader.GetString(10).Trim();

            if (reader.GetGuid(0) !=
                    expected.Id ||
                reader.GetInt32(1) !=
                    expected.Ordinal ||
                !string.Equals(
                    reader.GetString(2),
                    expected.Text,
                    StringComparison.Ordinal) ||
                reader.GetGuid(3) !=
                    expected.SegmentId ||
                reader.GetInt32(4) != 0 ||
                reader.GetInt32(5) !=
                    expected.StartOffset ||
                reader.GetInt32(6) !=
                    expected.EndOffset ||
                reader.GetGuid(7) !=
                    GetEmbeddingId(
                        package,
                        profile,
                        expected.Id) ||
                !string.Equals(
                    reader.GetString(8),
                    profile.EmbeddingProvider,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    reader.GetString(9),
                    profile.EmbeddingModel,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    persistedDigest,
                    modelDigest,
                    StringComparison.Ordinal) ||
                reader.GetInt32(11) !=
                    profile.EmbeddingDimensions ||
                reader.GetInt32(12) !=
                    profile.EmbeddingDimensions)
            {
                throw new InvalidOperationException(
                    $"Existing retrieval projection row {expected.Ordinal} does not match profile {profile.ProfileId}.");
            }
        }

        if (index != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Existing retrieval projection has {index} complete chunk/embedding rows; expected {chunks.Count}.");
        }
    }

    private static Guid GetEmbeddingId(
        KnowledgeImportPackage package,
        KnowledgeRetrievalProfile profile,
        Guid chunkId) =>
        KnowledgeStableIds.ForSourceProfile(
            package.StableIdNamespace,
            $"retrieval/{profile.ProfileId}/embedding/{chunkId:D}");

    private static string ToVectorLiteral(
        IReadOnlyList<float> values) =>
        "[" +
        string.Join(
            ",",
            values.Select(value =>
                value.ToString(
                    "R",
                    CultureInfo.InvariantCulture))) +
        "]";

    private static bool IsSha256(
        string value) =>
        value.Length == 64 &&
        value.All(character =>
            character is
                >= '0' and <= '9' or
                >= 'a' and <= 'f');

    private static bool NullableStringEquals(
        NpgsqlDataReader reader,
        int ordinal,
        string? expected) =>
        reader.IsDBNull(ordinal)
            ? expected is null
            : string.Equals(
                reader.GetString(ordinal),
                expected,
                StringComparison.Ordinal);

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command =
            new NpgsqlCommand(
                sql,
                connection,
                transaction);

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(
                parameter.Name,
                parameter.Value);
        }

        await command.ExecuteNonQueryAsync(
            cancellationToken);
    }
}
