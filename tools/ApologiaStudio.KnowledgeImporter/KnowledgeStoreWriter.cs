using Npgsql;

namespace ApologiaStudio.KnowledgeImporter;

internal sealed record KnowledgeImportResult(
    bool WasCreated,
    Guid WorkId,
    Guid NormalizedArtifactId,
    int SegmentCount);

internal static class KnowledgeStoreWriter
{
    private const string EditorialActor = "ApologiaStudio curated source manifest";

    private static readonly Guid VolumeWorkId = StableKnowledgeIds.ForProfile("volume-work");
    private static readonly Guid VolumeExpressionId = StableKnowledgeIds.ForProfile("volume-expression");
    private static readonly Guid VolumeManifestationId = StableKnowledgeIds.ForProfile("volume-manifestation");
    private static readonly Guid RawArtifactId = StableKnowledgeIds.ForProfile("raw-artifact");

    private static readonly Guid WorkId = StableKnowledgeIds.ForProfile("de-decretis-work");
    private static readonly Guid GreekExpressionId = StableKnowledgeIds.ForProfile("de-decretis-expression-grc");
    private static readonly Guid EnglishExpressionId = StableKnowledgeIds.ForProfile("de-decretis-expression-en");
    private static readonly Guid ManifestationId = StableKnowledgeIds.ForProfile("de-decretis-manifestation");
    private static readonly Guid ParsedArtifactId = StableKnowledgeIds.ForProfile("parsed-artifact");
    private static readonly Guid NormalizedArtifactId = StableKnowledgeIds.ForProfile("normalized-artifact");

    private static readonly Guid AthanasiusId = StableKnowledgeIds.ForAuthority("person:athanasius-of-alexandria");
    private static readonly Guid NewmanId = StableKnowledgeIds.ForAuthority("person:john-henry-newman");
    private static readonly Guid RobertsonId = StableKnowledgeIds.ForAuthority("person:archibald-robertson");

    public static async Task<KnowledgeImportResult> ImportAsync(
        string connectionString,
        PreparedDeDecretis prepared,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AcquireImportLockAsync(connection, transaction, cancellationToken);

        if (await ResourceExistsAsync(connection, transaction, WorkId, cancellationToken))
        {
            await ValidateExistingAsync(connection, transaction, prepared, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new KnowledgeImportResult(false, WorkId, NormalizedArtifactId, 32);
        }

        var now = DateTimeOffset.UtcNow;

        await EnsureContributorAsync(
            connection,
            transaction,
            AthanasiusId,
            "Athanasius of Alexandria",
            "Athanasius of Alexandria",
            "Author of De Decretis in the curated NPNF2-04 source profile.",
            now,
            cancellationToken);
        await EnsureContributorAsync(
            connection,
            transaction,
            NewmanId,
            "John Henry Newman",
            "Newman, John Henry",
            "Translator whose earlier English work is identified by the NPNF2-04 preface as the basis for this material.",
            now,
            cancellationToken);
        await EnsureContributorAsync(
            connection,
            transaction,
            RobertsonId,
            "Archibald Robertson",
            "Robertson, Archibald",
            "Editor of NPNF2-04; the volume preface describes revision of earlier translations and notes.",
            now,
            cancellationToken);

        var resourceIds = new List<Guid>
        {
            VolumeWorkId,
            VolumeExpressionId,
            VolumeManifestationId,
            RawArtifactId,
            WorkId,
            GreekExpressionId,
            EnglishExpressionId,
            ManifestationId,
            ParsedArtifactId,
            NormalizedArtifactId
        };
        resourceIds.AddRange(prepared.Segments.Select(x => x.Id));

        foreach (var resourceId in resourceIds)
        {
            await InsertResourceAsync(
                connection,
                transaction,
                resourceId,
                "approved",
                now,
                cancellationToken);
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_works
                (id, title, original_language, description)
            VALUES
                (@id, @title, NULL, @description)
            """,
            cancellationToken,
            ("id", VolumeWorkId),
            ("title", "NPNF2-04: Athanasius — Select Works and Letters"),
            ("description", "Editorial compilation represented by the complete CCEL PDF artifact."));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_expressions
                (id, work_id, language_code, label, description)
            VALUES
                (@id, @work_id, 'en', @label, @description)
            """,
            cancellationToken,
            ("id", VolumeExpressionId),
            ("work_id", VolumeWorkId),
            ("label", "English editorial compilation edited by Archibald Robertson"),
            ("description", "English NPNF editorial expression containing selected works and letters of Athanasius."));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_manifestations
                (id, expression_id, edition_statement, publication_year, publication_place, citation_label)
            VALUES
                (@id, @expression_id, @edition_statement, NULL, @publication_place, @citation_label)
            """,
            cancellationToken,
            ("id", VolumeManifestationId),
            ("expression_id", VolumeExpressionId),
            ("edition_statement", "Nicene and Post-Nicene Fathers, Second Series, Volume IV"),
            ("publication_place", "Edinburgh; Grand Rapids, Michigan"),
            ("citation_label", "NPNF2-04: Athanasius — Select Works and Letters"));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_manifestation_identifiers
                (manifestation_id, scheme, value, uri)
            VALUES
                (@manifestation_id, 'ccel', 'npnf204', @uri)
            """,
            cancellationToken,
            ("manifestation_id", VolumeManifestationId),
            ("uri", DeDecretisDocument.SourceUri));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_artifacts
                (id, manifestation_id, derived_from_artifact_id, artifact_type, sha256,
                 media_type, byte_length, origin_uri, acquired_at, lifecycle_status)
            VALUES
                (@id, @manifestation_id, NULL, 'raw', @sha256,
                 'application/pdf', @byte_length, @origin_uri, @acquired_at, 'active')
            """,
            cancellationToken,
            ("id", RawArtifactId),
            ("manifestation_id", VolumeManifestationId),
            ("sha256", prepared.RawSha256),
            ("byte_length", prepared.RawByteLength),
            ("origin_uri", DeDecretisDocument.SourceUri),
            ("acquired_at", now));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_works
                (id, title, original_language, description)
            VALUES
                (@id, @title, 'grc', @description)
            """,
            cancellationToken,
            ("id", WorkId),
            ("title", "De Decretis (Defence of the Nicene Definition)"),
            ("description", "Athanasius's defence and explanation of the Nicene definition."));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_expressions
                (id, work_id, language_code, label, description)
            VALUES
                (@id, @work_id, 'grc', @label, @description)
            """,
            cancellationToken,
            ("id", GreekExpressionId),
            ("work_id", WorkId),
            ("label", "Original Greek expression"),
            ("description", "Bibliographic expression record only; no Greek artifact is ingested in 6D."));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_expressions
                (id, work_id, language_code, label, description)
            VALUES
                (@id, @work_id, 'en', @label, @description)
            """,
            cancellationToken,
            ("id", EnglishExpressionId),
            ("work_id", WorkId),
            ("label", "NPNF English translation/revision"),
            ("description", "English translation represented in NPNF2-04, based on Newman's earlier translation and revised for this volume."));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_expression_relations
                (from_expression_id, to_expression_id, relation_type)
            VALUES
                (@from_id, @to_id, 'translation_of')
            """,
            cancellationToken,
            ("from_id", EnglishExpressionId),
            ("to_id", GreekExpressionId));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_manifestations
                (id, expression_id, edition_statement, publication_year, publication_place, citation_label)
            VALUES
                (@id, @expression_id, @edition_statement, NULL, @publication_place, @citation_label)
            """,
            cancellationToken,
            ("id", ManifestationId),
            ("expression_id", EnglishExpressionId),
            ("edition_statement", "De Decretis as contained in NPNF Second Series, Volume IV"),
            ("publication_place", "Edinburgh; Grand Rapids, Michigan"),
            ("citation_label", "NPNF2-04, De Decretis"));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_artifacts
                (id, manifestation_id, derived_from_artifact_id, artifact_type, sha256,
                 media_type, byte_length, origin_uri, acquired_at, lifecycle_status)
            VALUES
                (@id, @manifestation_id, @derived_from, 'parsed', @sha256,
                 'text/plain; charset=utf-8', @byte_length, NULL, @acquired_at, 'active')
            """,
            cancellationToken,
            ("id", ParsedArtifactId),
            ("manifestation_id", ManifestationId),
            ("derived_from", RawArtifactId),
            ("sha256", prepared.ParsedArtifact.Sha256),
            ("byte_length", prepared.ParsedArtifact.Bytes.LongLength),
            ("acquired_at", now));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_processing_activities
                (input_artifact_id, output_artifact_id, activity_type, tool_name, tool_version,
                 configuration_json, started_at, completed_at, executed_by, status)
            VALUES
                (@input_id, @output_id, 'parse', 'PdfPig', '0.1.15',
                 @configuration::jsonb, @started_at, @completed_at, @executed_by, 'completed')
            """,
            cancellationToken,
            ("input_id", RawArtifactId),
            ("output_id", ParsedArtifactId),
            ("configuration", BuildParserConfigurationJson()),
            ("started_at", now),
            ("completed_at", now),
            ("executed_by", "ApologiaStudio.KnowledgeImporter"));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_artifacts
                (id, manifestation_id, derived_from_artifact_id, artifact_type, sha256,
                 media_type, byte_length, origin_uri, acquired_at, lifecycle_status)
            VALUES
                (@id, @manifestation_id, @derived_from, 'normalized', @sha256,
                 'text/plain; charset=utf-8', @byte_length, NULL, @acquired_at, 'active')
            """,
            cancellationToken,
            ("id", NormalizedArtifactId),
            ("manifestation_id", ManifestationId),
            ("derived_from", ParsedArtifactId),
            ("sha256", prepared.NormalizedArtifact.Sha256),
            ("byte_length", prepared.NormalizedArtifact.Bytes.LongLength),
            ("acquired_at", now));

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_processing_activities
                (input_artifact_id, output_artifact_id, activity_type, tool_name, tool_version,
                 configuration_json, started_at, completed_at, executed_by, status)
            VALUES
                (@input_id, @output_id, 'normalize', 'ApologiaStudio.KnowledgeImporter', @tool_version,
                 @configuration::jsonb, @started_at, @completed_at, @executed_by, 'completed')
            """,
            cancellationToken,
            ("input_id", ParsedArtifactId),
            ("output_id", NormalizedArtifactId),
            ("tool_version", DeDecretisDocument.ProfileId),
            ("configuration", "{\"chapterHeadings\":\"excluded\",\"lineBreakHyphens\":\"preserved\",\"unicodeNormalization\":\"NFC\"}"),
            ("started_at", now),
            ("completed_at", now),
            ("executed_by", "ApologiaStudio.KnowledgeImporter"));

        await InsertContributionAsync(
            connection,
            transaction,
            AthanasiusId,
            WorkId,
            null,
            null,
            "author",
            "established",
            0,
            cancellationToken);
        await InsertContributionAsync(
            connection,
            transaction,
            NewmanId,
            null,
            EnglishExpressionId,
            null,
            "translator",
            "established",
            0,
            cancellationToken);
        await InsertContributionAsync(
            connection,
            transaction,
            RobertsonId,
            null,
            EnglishExpressionId,
            null,
            "reviser",
            "explicit",
            1,
            cancellationToken);
        await InsertContributionAsync(
            connection,
            transaction,
            RobertsonId,
            null,
            VolumeExpressionId,
            null,
            "textual_editor",
            "explicit",
            0,
            cancellationToken);

        foreach (var segment in prepared.Segments)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO knowledge_document_segments
                    (id, artifact_id, parent_segment_id, segment_type, segment_kind, ordinal, title, text, locator)
                VALUES
                    (@id, @artifact_id, NULL, 'section', 'main_text', @ordinal, @title, @text, @locator)
                """,
                cancellationToken,
                ("id", segment.Id),
                ("artifact_id", NormalizedArtifactId),
                ("ordinal", segment.Number),
                ("title", $"Section {segment.Number}"),
                ("text", segment.Text),
                ("locator", segment.Locator));
        }

        var sourceKindId = await EnsureSourceKindAsync(
            connection,
            transaction,
            "primary_source",
            "Primary source",
            "A source produced by a historical participant or witness relevant to the question under study.",
            cancellationToken);
        var perspectiveId = await EnsurePerspectiveAsync(
            connection,
            transaction,
            "pro_nicene",
            "Pro-Nicene",
            "Analytical classification for fourth-century material defending the Nicene settlement.",
            "Fourth century",
            cancellationToken);
        var historicalWitnessRoleId = await EnsureEvidenceRoleAsync(
            connection,
            transaction,
            "historical_witness",
            "Historical witness",
            "Evidence for what a historical actor reports, argues, or remembers.",
            cancellationToken);
        var theologicalArgumentRoleId = await EnsureEvidenceRoleAsync(
            connection,
            transaction,
            "theological_argument",
            "Theological argument",
            "Evidence used to analyze a theological argument in its historical source.",
            cancellationToken);

        await InsertSourceKindAssertionAsync(
            connection,
            transaction,
            StableKnowledgeIds.ForProfile("assertion:source-kind:primary-source"),
            WorkId,
            sourceKindId,
            "De Decretis is authored by Athanasius and is used here as a primary source for his fourth-century Nicene argument.",
            now,
            cancellationToken);
        await InsertPerspectiveAssertionAsync(
            connection,
            transaction,
            StableKnowledgeIds.ForProfile("assertion:perspective:pro-nicene"),
            WorkId,
            perspectiveId,
            "analytical",
            "Editorial classification based on the work's explicit defence of the Nicene definition.",
            now,
            cancellationToken);
        await InsertEvidenceRoleAssertionAsync(
            connection,
            transaction,
            StableKnowledgeIds.ForProfile("assertion:evidence-role:historical-witness"),
            WorkId,
            historicalWitnessRoleId,
            "The work contains Athanasius's account of the Nicene controversy and proceedings.",
            now,
            cancellationToken);
        await InsertEvidenceRoleAssertionAsync(
            connection,
            transaction,
            StableKnowledgeIds.ForProfile("assertion:evidence-role:theological-argument"),
            WorkId,
            theologicalArgumentRoleId,
            "The work explicitly argues for the wording and meaning of the Nicene definition.",
            now,
            cancellationToken);

        await InsertMetadataAssertionAsync(
            connection,
            transaction,
            StableKnowledgeIds.ForProfile("assertion:raw:pdf-page-count"),
            RawArtifactId,
            "pdf_page_count",
            DeDecretisDocument.ExpectedPdfPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "imported",
            "Verified from the acquired PDF artifact during ingestion.",
            now,
            cancellationToken);
        await InsertMetadataAssertionAsync(
            connection,
            transaction,
            StableKnowledgeIds.ForProfile("assertion:normalized:source-page-range"),
            NormalizedArtifactId,
            "source_pdf_page_range",
            $"{DeDecretisDocument.FirstPdfPage}-{DeDecretisDocument.LastPdfPage}",
            "imported",
            "The selected PDF pages correspond to printed NPNF pages 482–531 and end before De Sententia Dionysii.",
            now,
            cancellationToken);

        await ValidateExistingAsync(connection, transaction, prepared, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new KnowledgeImportResult(true, WorkId, NormalizedArtifactId, prepared.Segments.Count);
    }

    public static async Task<IReadOnlySet<string>> RemoveAsync(
        string connectionString,
        PreparedDeDecretis prepared,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AcquireImportLockAsync(connection, transaction, cancellationToken);

        var segmentIds = prepared.Segments.Select(x => x.Id).ToArray();

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_source_kind_assertions WHERE resource_id = @work_id",
            cancellationToken,
            ("work_id", WorkId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_perspective_assertions WHERE resource_id = @work_id",
            cancellationToken,
            ("work_id", WorkId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_evidence_role_assertions WHERE resource_id = @work_id",
            cancellationToken,
            ("work_id", WorkId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_metadata_assertions WHERE resource_id IN (@raw_id, @normalized_id)",
            cancellationToken,
            ("raw_id", RawArtifactId),
            ("normalized_id", NormalizedArtifactId));

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_processing_activities WHERE output_artifact_id IN (@parsed_id, @normalized_id)",
            cancellationToken,
            ("parsed_id", ParsedArtifactId),
            ("normalized_id", NormalizedArtifactId));

        // Retrieval chunks are rebuildable projections over segments. Remove them first
        // so their restrictive segment mappings cannot block deletion of citable segments.
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_retrieval_chunks WHERE artifact_id = @normalized_id",
            cancellationToken,
            ("normalized_id", NormalizedArtifactId));

        foreach (var segmentId in segmentIds)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_document_segments WHERE id = @id",
                cancellationToken,
                ("id", segmentId));
        }

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_artifacts WHERE id = @id",
            cancellationToken,
            ("id", NormalizedArtifactId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_artifacts WHERE id = @id",
            cancellationToken,
            ("id", ParsedArtifactId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_artifacts WHERE id = @id",
            cancellationToken,
            ("id", RawArtifactId));

        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM knowledge_contributions
            WHERE work_id IN (@work_id, @volume_work_id)
               OR expression_id IN (@english_expression_id, @volume_expression_id)
               OR manifestation_id IN (@manifestation_id, @volume_manifestation_id)
            """,
            cancellationToken,
            ("work_id", WorkId),
            ("volume_work_id", VolumeWorkId),
            ("english_expression_id", EnglishExpressionId),
            ("volume_expression_id", VolumeExpressionId),
            ("manifestation_id", ManifestationId),
            ("volume_manifestation_id", VolumeManifestationId));

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_manifestation_identifiers WHERE manifestation_id = @id",
            cancellationToken,
            ("id", VolumeManifestationId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_manifestations WHERE id IN (@de_id, @volume_id)",
            cancellationToken,
            ("de_id", ManifestationId),
            ("volume_id", VolumeManifestationId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_expression_relations WHERE from_expression_id = @id OR to_expression_id = @id2",
            cancellationToken,
            ("id", EnglishExpressionId),
            ("id2", GreekExpressionId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_expressions WHERE id IN (@greek_id, @english_id, @volume_id)",
            cancellationToken,
            ("greek_id", GreekExpressionId),
            ("english_id", EnglishExpressionId),
            ("volume_id", VolumeExpressionId));
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM knowledge_works WHERE id IN (@work_id, @volume_id)",
            cancellationToken,
            ("work_id", WorkId),
            ("volume_id", VolumeWorkId));

        var sourceSpecificResources = new List<Guid>
        {
            VolumeWorkId,
            VolumeExpressionId,
            VolumeManifestationId,
            RawArtifactId,
            WorkId,
            GreekExpressionId,
            EnglishExpressionId,
            ManifestationId,
            ParsedArtifactId,
            NormalizedArtifactId
        };
        sourceSpecificResources.AddRange(segmentIds);

        foreach (var resourceId in sourceSpecificResources)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_resources WHERE id = @id",
                cancellationToken,
                ("id", resourceId));
        }

        foreach (var contributorId in new[] { AthanasiusId, NewmanId, RobertsonId })
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                DELETE FROM knowledge_contributors c
                WHERE c.id = @id
                  AND NOT EXISTS (
                      SELECT 1 FROM knowledge_contributions contribution
                      WHERE contribution.contributor_id = c.id)
                """,
                cancellationToken,
                ("id", contributorId));
            await ExecuteAsync(
                connection,
                transaction,
                """
                DELETE FROM knowledge_resources r
                WHERE r.id = @id
                  AND NOT EXISTS (
                      SELECT 1 FROM knowledge_contributors c
                      WHERE c.id = r.id)
                """,
                cancellationToken,
                ("id", contributorId));
        }

        await DeleteUnusedControlledTermsAsync(connection, transaction, cancellationToken);

        var hashes = new[]
        {
            prepared.RawSha256,
            prepared.ParsedArtifact.Sha256,
            prepared.NormalizedArtifact.Sha256
        };
        var deletable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hash in hashes)
        {
            if (!await ArtifactHashExistsAsync(connection, transaction, hash, cancellationToken))
            {
                deletable.Add(hash);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return deletable;
    }

    private static async Task ValidateExistingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PreparedDeDecretis prepared,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                w.title,
                raw.sha256,
                parsed.sha256,
                normalized.sha256,
                COUNT(segment.id),
                MIN(segment.ordinal),
                MAX(segment.ordinal),
                COUNT(DISTINCT segment.ordinal)
            FROM knowledge_works w
            JOIN knowledge_expressions e ON e.work_id = w.id AND e.id = @english_expression_id
            JOIN knowledge_manifestations m ON m.expression_id = e.id AND m.id = @manifestation_id
            JOIN knowledge_artifacts raw ON raw.id = @raw_artifact_id
            JOIN knowledge_artifacts parsed
              ON parsed.id = @parsed_artifact_id
             AND parsed.manifestation_id = m.id
             AND parsed.derived_from_artifact_id = raw.id
            JOIN knowledge_artifacts normalized
              ON normalized.id = @normalized_artifact_id
             AND normalized.manifestation_id = m.id
             AND normalized.derived_from_artifact_id = parsed.id
            LEFT JOIN knowledge_document_segments segment ON segment.artifact_id = normalized.id
            WHERE w.id = @work_id
            GROUP BY w.title, raw.sha256, parsed.sha256, normalized.sha256
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("english_expression_id", EnglishExpressionId);
        command.Parameters.AddWithValue("manifestation_id", ManifestationId);
        command.Parameters.AddWithValue("parsed_artifact_id", ParsedArtifactId);
        command.Parameters.AddWithValue("normalized_artifact_id", NormalizedArtifactId);
        command.Parameters.AddWithValue("raw_artifact_id", RawArtifactId);
        command.Parameters.AddWithValue("work_id", WorkId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new KnowledgeImportException(
                "The De Decretis Knowledge chain is incomplete after import.");
        }

        var title = reader.GetString(0);
        var rawSha = reader.GetString(1).Trim();
        var parsedSha = reader.GetString(2).Trim();
        var normalizedSha = reader.GetString(3).Trim();
        var segmentCount = reader.GetInt64(4);
        var minimumOrdinal = reader.GetInt32(5);
        var maximumOrdinal = reader.GetInt32(6);
        var distinctOrdinalCount = reader.GetInt64(7);

        if (!string.Equals(
                title,
                "De Decretis (Defence of the Nicene Definition)",
                StringComparison.Ordinal) ||
            !string.Equals(rawSha, prepared.RawSha256, StringComparison.Ordinal) ||
            !string.Equals(parsedSha, prepared.ParsedArtifact.Sha256, StringComparison.Ordinal) ||
            !string.Equals(normalizedSha, prepared.NormalizedArtifact.Sha256, StringComparison.Ordinal) ||
            segmentCount != 32 ||
            minimumOrdinal != 1 ||
            maximumOrdinal != 32 ||
            distinctOrdinalCount != 32)
        {
            throw new KnowledgeImportException(
                "An existing De Decretis import does not match the current curated profile.");
        }
    }

    private static async Task EnsureSchemaAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT
                to_regclass('public.knowledge_works') IS NOT NULL
                AND to_regclass('public.knowledge_artifacts') IS NOT NULL
                AND to_regclass('public.knowledge_document_segments') IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" LIKE '%_InitialKnowledgePersistence')
            """,
            connection);

        var valid = await command.ExecuteScalarAsync(cancellationToken);
        if (valid is not true)
        {
            throw new KnowledgeImportException(
                "Knowledge Store schema is not ready. Apply Knowledge migrations before importing.");
        }
    }

    private static async Task AcquireImportLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext(@key))",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", DeDecretisDocument.ProfileId);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<bool> ResourceExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM knowledge_resources WHERE id = @id)",
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<bool> ArtifactHashExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sha256,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM knowledge_artifacts WHERE sha256 = @sha256)",
            connection,
            transaction);
        command.Parameters.AddWithValue("sha256", sha256);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task InsertResourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string reviewStatus,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_resources
                (id, editorial_review_status, created_at)
            VALUES
                (@id, @review_status, @created_at)
            """,
            cancellationToken,
            ("id", id),
            ("review_status", reviewStatus),
            ("created_at", createdAt));
    }

    private static async Task EnsureContributorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string preferredName,
        string sortName,
        string description,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using (var query = new NpgsqlCommand(
                         "SELECT preferred_name FROM knowledge_contributors WHERE id = @id",
                         connection,
                         transaction))
        {
            query.Parameters.AddWithValue("id", id);
            var existing = await query.ExecuteScalarAsync(cancellationToken);
            if (existing is string existingName)
            {
                if (!string.Equals(existingName, preferredName, StringComparison.Ordinal))
                {
                    throw new KnowledgeImportException(
                        $"Contributor identity collision for {preferredName}.");
                }

                return;
            }
        }

        await InsertResourceAsync(
            connection,
            transaction,
            id,
            "approved",
            now,
            cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_contributors
                (id, contributor_type, preferred_name, sort_name, description)
            VALUES
                (@id, 'person', @preferred_name, @sort_name, @description)
            """,
            cancellationToken,
            ("id", id),
            ("preferred_name", preferredName),
            ("sort_name", sortName),
            ("description", description));
    }

    private static Task InsertContributionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid contributorId,
        Guid? workId,
        Guid? expressionId,
        Guid? manifestationId,
        string role,
        string attributionStatus,
        int ordinal,
        CancellationToken cancellationToken)
    {
        var targetCount =
            (workId.HasValue ? 1 : 0) +
            (expressionId.HasValue ? 1 : 0) +
            (manifestationId.HasValue ? 1 : 0);

        if (targetCount != 1)
        {
            throw new ArgumentException(
                "A contribution must target exactly one Work, Expression, or Manifestation.");
        }

        if (workId.HasValue)
        {
            return ExecuteContributionAsync(
                connection,
                transaction,
                "@target_id, NULL, NULL",
                contributorId,
                workId.Value,
                role,
                attributionStatus,
                ordinal,
                cancellationToken);
        }

        if (expressionId.HasValue)
        {
            return ExecuteContributionAsync(
                connection,
                transaction,
                "NULL, @target_id, NULL",
                contributorId,
                expressionId.Value,
                role,
                attributionStatus,
                ordinal,
                cancellationToken);
        }

        return ExecuteContributionAsync(
            connection,
            transaction,
            "NULL, NULL, @target_id",
            contributorId,
            manifestationId!.Value,
            role,
            attributionStatus,
            ordinal,
            cancellationToken);
    }

    private static Task ExecuteContributionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string targetSql,
        Guid contributorId,
        Guid targetId,
        string role,
        string attributionStatus,
        int ordinal,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            $"""
            INSERT INTO knowledge_contributions
                (contributor_id, work_id, expression_id, manifestation_id,
                 role, attribution_status, ordinal)
            VALUES
                (@contributor_id, {targetSql}, @role, @attribution_status, @ordinal)
            """,
            cancellationToken,
            ("contributor_id", contributorId),
            ("target_id", targetId),
            ("role", role),
            ("attribution_status", attributionStatus),
            ("ordinal", ordinal));

    private static async Task<Guid> EnsureSourceKindAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string code,
        string label,
        string description,
        CancellationToken cancellationToken) =>
        await EnsureControlledTermAsync(
            connection,
            transaction,
            "knowledge_source_kinds",
            "source-kind:" + code,
            code,
            label,
            description,
            cancellationToken);

    private static async Task<Guid> EnsureEvidenceRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string code,
        string label,
        string description,
        CancellationToken cancellationToken) =>
        await EnsureControlledTermAsync(
            connection,
            transaction,
            "knowledge_evidence_roles",
            "evidence-role:" + code,
            code,
            label,
            description,
            cancellationToken);

    private static async Task<Guid> EnsurePerspectiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string code,
        string label,
        string description,
        string historicalPeriod,
        CancellationToken cancellationToken)
    {
        await using (var query = new NpgsqlCommand(
                         "SELECT id FROM knowledge_perspectives WHERE code = @code",
                         connection,
                         transaction))
        {
            query.Parameters.AddWithValue("code", code);
            var existing = await query.ExecuteScalarAsync(cancellationToken);
            if (existing is Guid existingId)
            {
                return existingId;
            }
        }

        var id = StableKnowledgeIds.ForVocabulary("perspective:" + code);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_perspectives
                (id, code, label, parent_perspective_id, description, historical_period)
            VALUES
                (@id, @code, @label, NULL, @description, @historical_period)
            """,
            cancellationToken,
            ("id", id),
            ("code", code),
            ("label", label),
            ("description", description),
            ("historical_period", historicalPeriod));
        return id;
    }

    private static async Task<Guid> EnsureControlledTermAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table,
        string idName,
        string code,
        string label,
        string description,
        CancellationToken cancellationToken)
    {
        await using (var query = new NpgsqlCommand(
                         $"SELECT id FROM {table} WHERE code = @code",
                         connection,
                         transaction))
        {
            query.Parameters.AddWithValue("code", code);
            var existing = await query.ExecuteScalarAsync(cancellationToken);
            if (existing is Guid existingId)
            {
                return existingId;
            }
        }

        var id = StableKnowledgeIds.ForVocabulary(idName);
        await ExecuteAsync(
            connection,
            transaction,
            $"INSERT INTO {table} (id, code, label, description) VALUES (@id, @code, @label, @description)",
            cancellationToken,
            ("id", id),
            ("code", code),
            ("label", label),
            ("description", description));
        return id;
    }

    private static Task InsertSourceKindAssertionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid resourceId,
        Guid sourceKindId,
        string justification,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_source_kind_assertions
                (id, resource_id, source_kind_id, assertion_origin, asserted_by, asserted_at,
                 review_status, reviewed_by, reviewed_at, justification,
                 supporting_segment_id, supersedes_assertion_id)
            VALUES
                (@id, @resource_id, @term_id, 'editorial', @actor, @now,
                 'verified', @actor, @now, @justification, NULL, NULL)
            """,
            cancellationToken,
            ("id", id),
            ("resource_id", resourceId),
            ("term_id", sourceKindId),
            ("actor", EditorialActor),
            ("now", now),
            ("justification", justification));

    private static Task InsertPerspectiveAssertionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid resourceId,
        Guid perspectiveId,
        string perspectiveType,
        string justification,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_perspective_assertions
                (id, resource_id, perspective_id, perspective_type, assertion_origin,
                 asserted_by, asserted_at, review_status, reviewed_by, reviewed_at,
                 justification, supporting_segment_id, supersedes_assertion_id)
            VALUES
                (@id, @resource_id, @term_id, @perspective_type, 'editorial',
                 @actor, @now, 'verified', @actor, @now,
                 @justification, NULL, NULL)
            """,
            cancellationToken,
            ("id", id),
            ("resource_id", resourceId),
            ("term_id", perspectiveId),
            ("perspective_type", perspectiveType),
            ("actor", EditorialActor),
            ("now", now),
            ("justification", justification));

    private static Task InsertEvidenceRoleAssertionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid resourceId,
        Guid evidenceRoleId,
        string justification,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_evidence_role_assertions
                (id, resource_id, evidence_role_id, assertion_origin, asserted_by, asserted_at,
                 review_status, reviewed_by, reviewed_at, justification,
                 supporting_segment_id, supersedes_assertion_id)
            VALUES
                (@id, @resource_id, @term_id, 'editorial', @actor, @now,
                 'verified', @actor, @now, @justification, NULL, NULL)
            """,
            cancellationToken,
            ("id", id),
            ("resource_id", resourceId),
            ("term_id", evidenceRoleId),
            ("actor", EditorialActor),
            ("now", now),
            ("justification", justification));

    private static Task InsertMetadataAssertionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid resourceId,
        string property,
        string value,
        string origin,
        string justification,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_metadata_assertions
                (id, resource_id, property, value, assertion_origin, asserted_by, asserted_at,
                 review_status, reviewed_by, reviewed_at, confidence, justification,
                 supporting_segment_id, supersedes_assertion_id)
            VALUES
                (@id, @resource_id, @property, @value, @origin, @actor, @now,
                 'verified', @actor, @now, NULL, @justification, NULL, NULL)
            """,
            cancellationToken,
            ("id", id),
            ("resource_id", resourceId),
            ("property", property),
            ("value", value),
            ("origin", origin),
            ("actor", EditorialActor),
            ("now", now),
            ("justification", justification));

    private static async Task DeleteUnusedControlledTermsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM knowledge_source_kinds term
            WHERE term.code = 'primary_source'
              AND NOT EXISTS (
                  SELECT 1 FROM knowledge_source_kind_assertions a
                  WHERE a.source_kind_id = term.id)
            """,
            cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM knowledge_perspectives term
            WHERE term.code = 'pro_nicene'
              AND NOT EXISTS (
                  SELECT 1 FROM knowledge_perspective_assertions a
                  WHERE a.perspective_id = term.id)
            """,
            cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM knowledge_evidence_roles term
            WHERE term.code IN ('historical_witness', 'theological_argument')
              AND NOT EXISTS (
                  SELECT 1 FROM knowledge_evidence_role_assertions a
                  WHERE a.evidence_role_id = term.id)
            """,
            cancellationToken);
    }

    private static string BuildParserConfigurationJson() =>
        $$"""
        {
          "profile": "{{DeDecretisDocument.ProfileId}}",
          "pdfPageStart": {{DeDecretisDocument.FirstPdfPage}},
          "pdfPageEnd": {{DeDecretisDocument.LastPdfPage}},
          "minimumFontSize": {{DeDecretisDocument.MinimumFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "minimumBaselineY": {{DeDecretisDocument.MinimumBaselineY.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "maximumBaselineY": {{DeDecretisDocument.MaximumBaselineY.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "excluded": ["running headers", "page numbers", "editorial footnotes"]
        }
        """;

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
