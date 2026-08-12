using ApologiaStudio.Application.Knowledge.Ingestion;
using Npgsql;

namespace ApologiaStudio.Infrastructure.Knowledge.Ingestion;

public static class PostgreSqlKnowledgeImportStore
{
    public static async Task<KnowledgeImportResult> ImportAsync(
        string connectionString,
        KnowledgeImportPackage package,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        KnowledgeImportPackageValidator.Validate(package);

        await using var connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        await AcquireImportLockAsync(
            connection,
            transaction,
            package.ProfileId,
            cancellationToken);

        if (await ResourceExistsAsync(
                connection,
                transaction,
                package.PrimaryWorkId,
                cancellationToken))
        {
            await ValidateExistingAsync(
                connection,
                transaction,
                package,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new KnowledgeImportResult(
                false,
                package.PrimaryWorkId,
                package.NormalizedArtifactId,
                package.Segments.Count);
        }

        await EnsureNoPartialPackageAsync(
            connection,
            transaction,
            package,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        foreach (var contributor in package.Contributors)
        {
            await EnsureContributorAsync(
                connection,
                transaction,
                contributor,
                now,
                cancellationToken);
        }

        foreach (var resource in GetOwnedResources(package))
        {
            await InsertResourceAsync(
                connection,
                transaction,
                resource.Id,
                resource.ReviewStatus,
                now,
                cancellationToken);
        }

        foreach (var work in package.Works)
        {
            await InsertWorkAsync(
                connection,
                transaction,
                work,
                cancellationToken);
        }

        foreach (var expression in package.Expressions)
        {
            await InsertExpressionAsync(
                connection,
                transaction,
                expression,
                cancellationToken);
        }

        foreach (var relation in package.ExpressionRelations)
        {
            await InsertExpressionRelationAsync(
                connection,
                transaction,
                relation,
                cancellationToken);
        }

        foreach (var manifestation in package.Manifestations)
        {
            await InsertManifestationAsync(
                connection,
                transaction,
                manifestation,
                cancellationToken);
        }

        foreach (var identifier in package.ManifestationIdentifiers)
        {
            await InsertManifestationIdentifierAsync(
                connection,
                transaction,
                identifier,
                cancellationToken);
        }

        foreach (var artifact in package.Artifacts)
        {
            await InsertArtifactAsync(
                connection,
                transaction,
                artifact,
                now,
                cancellationToken);
        }

        foreach (var activity in package.ProcessingActivities)
        {
            await InsertProcessingActivityAsync(
                connection,
                transaction,
                activity,
                now,
                cancellationToken);
        }

        foreach (var contribution in package.Contributions)
        {
            await InsertContributionAsync(
                connection,
                transaction,
                contribution,
                cancellationToken);
        }

        foreach (var segment in package.Segments)
        {
            await InsertSegmentAsync(
                connection,
                transaction,
                segment,
                cancellationToken);
        }

        var termIds =
            await EnsureClassificationTermsAsync(
                connection,
                transaction,
                package.ClassificationTerms,
                cancellationToken);

        foreach (var assertion in package.ClassificationAssertions)
        {
            await InsertClassificationAssertionAsync(
                connection,
                transaction,
                assertion,
                termIds[
                    (assertion.Dimension, assertion.TermCode)],
                now,
                cancellationToken);
        }

        foreach (var assertion in package.MetadataAssertions)
        {
            await InsertMetadataAssertionAsync(
                connection,
                transaction,
                assertion,
                now,
                cancellationToken);
        }

        await ValidateExistingAsync(
            connection,
            transaction,
            package,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new KnowledgeImportResult(
            true,
            package.PrimaryWorkId,
            package.NormalizedArtifactId,
            package.Segments.Count);
    }

    public static async Task<IReadOnlySet<string>> RemoveAsync(
        string connectionString,
        KnowledgeImportPackage package,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        KnowledgeImportPackageValidator.Validate(package);

        await using var connection =
            new NpgsqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        await AcquireImportLockAsync(
            connection,
            transaction,
            package.ProfileId,
            cancellationToken);

        foreach (var assertion in
                 package.ClassificationAssertions.Reverse())
        {
            await DeleteClassificationAssertionAsync(
                connection,
                transaction,
                assertion,
                cancellationToken);
        }

        foreach (var assertion in
                 package.MetadataAssertions.Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_metadata_assertions WHERE id = @id",
                cancellationToken,
                ("id", assertion.Id));
        }

        foreach (var artifact in
                 package.Artifacts.Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_processing_activities WHERE output_artifact_id = @artifact_id",
                cancellationToken,
                ("artifact_id", artifact.Id));

            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_retrieval_chunks WHERE artifact_id = @artifact_id",
                cancellationToken,
                ("artifact_id", artifact.Id));
        }

        foreach (var segment in package.Segments.Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_document_segments WHERE id = @id",
                cancellationToken,
                ("id", segment.Id));
        }

        foreach (var artifact in package.Artifacts.Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_artifacts WHERE id = @id",
                cancellationToken,
                ("id", artifact.Id));
        }

        foreach (var contribution in
                 package.Contributions.Reverse())
        {
            await DeleteContributionAsync(
                connection,
                transaction,
                contribution,
                cancellationToken);
        }

        foreach (var identifier in
                 package.ManifestationIdentifiers.Reverse())
        {
            await DeleteManifestationIdentifierAsync(
                connection,
                transaction,
                identifier,
                cancellationToken);
        }

        foreach (var manifestation in
                 package.Manifestations.Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_manifestations WHERE id = @id",
                cancellationToken,
                ("id", manifestation.Id));
        }

        foreach (var relation in
                 package.ExpressionRelations.Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                DELETE FROM knowledge_expression_relations
                WHERE from_expression_id = @from_id
                  AND to_expression_id = @to_id
                  AND relation_type = @relation_type
                """,
                cancellationToken,
                ("from_id", relation.FromExpressionId),
                ("to_id", relation.ToExpressionId),
                ("relation_type", relation.RelationType));
        }

        foreach (var expression in
                 package.Expressions.Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_expressions WHERE id = @id",
                cancellationToken,
                ("id", expression.Id));
        }

        foreach (var work in package.Works.Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_works WHERE id = @id",
                cancellationToken,
                ("id", work.Id));
        }

        foreach (var resource in
                 GetOwnedResources(package).Reverse())
        {
            await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM knowledge_resources WHERE id = @id",
                cancellationToken,
                ("id", resource.Id));
        }

        foreach (var contributor in
                 package.Contributors.Reverse())
        {
            await DeleteContributorIfUnusedAsync(
                connection,
                transaction,
                contributor.Id,
                cancellationToken);
        }

        await DeleteUnusedClassificationTermsAsync(
            connection,
            transaction,
            package.ClassificationTerms,
            cancellationToken);

        var deletable =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (var hash in package.Artifacts
                     .Select(artifact => artifact.Sha256)
                     .Distinct(StringComparer.Ordinal))
        {
            if (!await ArtifactHashExistsAsync(
                    connection,
                    transaction,
                    hash,
                    cancellationToken))
            {
                deletable.Add(hash);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return deletable;
    }

    internal static string ToDatabaseValue(
        DocumentSegmentType type) =>
        type switch
        {
            DocumentSegmentType.Unknown => "unknown",
            DocumentSegmentType.Chapter => "chapter",
            DocumentSegmentType.Section => "section",
            DocumentSegmentType.Subsection => "subsection",
            DocumentSegmentType.ParagraphGroup => "paragraph_group",
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "Unsupported document segment type.")
        };

    internal static string ToDatabaseValue(
        DocumentSegmentKind kind) =>
        kind switch
        {
            DocumentSegmentKind.Unknown => "unknown",
            DocumentSegmentKind.MainText => "main_text",
            DocumentSegmentKind.PedagogicalPrompt => "pedagogical_prompt",
            DocumentSegmentKind.Sidebar => "sidebar",
            DocumentSegmentKind.Bibliography => "bibliography",
            DocumentSegmentKind.Caption => "caption",
            DocumentSegmentKind.Glossary => "glossary",
            DocumentSegmentKind.Index => "index",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported document segment kind.")
        };

    private static IReadOnlyList<OwnedResource>
        GetOwnedResources(
            KnowledgeImportPackage package)
    {
        var resources = new List<OwnedResource>(
            package.Works.Count +
            package.Expressions.Count +
            package.Manifestations.Count +
            package.Artifacts.Count +
            package.Segments.Count);

        resources.AddRange(
            package.Works.Select(item =>
                new OwnedResource(
                    item.Id,
                    item.EditorialReviewStatus)));
        resources.AddRange(
            package.Expressions.Select(item =>
                new OwnedResource(
                    item.Id,
                    item.EditorialReviewStatus)));
        resources.AddRange(
            package.Manifestations.Select(item =>
                new OwnedResource(
                    item.Id,
                    item.EditorialReviewStatus)));
        resources.AddRange(
            package.Artifacts.Select(item =>
                new OwnedResource(
                    item.Id,
                    item.EditorialReviewStatus)));
        resources.AddRange(
            package.Segments.Select(item =>
                new OwnedResource(
                    item.Id,
                    item.EditorialReviewStatus)));

        return resources;
    }

    private static async Task EnsureNoPartialPackageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportPackage package,
        CancellationToken cancellationToken)
    {
        foreach (var resource in GetOwnedResources(package))
        {
            if (await ResourceExistsAsync(
                    connection,
                    transaction,
                    resource.Id,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Import profile {package.ProfileId} is only partially present. " +
                    $"Unexpected existing resource: {resource.Id}.");
            }
        }
    }

    private static async Task ValidateExistingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportPackage package,
        CancellationToken cancellationToken)
    {
        foreach (var resource in GetOwnedResources(package))
        {
            if (!await ResourceExistsAsync(
                    connection,
                    transaction,
                    resource.Id,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Import profile {package.ProfileId} is incomplete. " +
                    $"Missing resource: {resource.Id}.");
            }
        }

        var expectedPrimaryWork =
            package.Works.Single(work =>
                work.Id == package.PrimaryWorkId);

        await using (var command =
                     new NpgsqlCommand(
                         """
                         SELECT title
                         FROM knowledge_works
                         WHERE id = @id
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue(
                "id",
                package.PrimaryWorkId);

            var title =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (title is not string persistedTitle ||
                !string.Equals(
                    persistedTitle,
                    expectedPrimaryWork.Title,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Existing primary work does not match import profile {package.ProfileId}.");
            }
        }

        foreach (var artifact in package.Artifacts)
        {
            await ValidateArtifactAsync(
                connection,
                transaction,
                artifact,
                cancellationToken);
        }

        foreach (var segment in package.Segments)
        {
            await ValidateSegmentAsync(
                connection,
                transaction,
                segment,
                cancellationToken);
        }

        foreach (var artifact in package.Artifacts)
        {
            await using var countCommand =
                new NpgsqlCommand(
                    """
                    SELECT COUNT(*)
                    FROM knowledge_document_segments
                    WHERE artifact_id = @artifact_id
                    """,
                    connection,
                    transaction);

            countCommand.Parameters.AddWithValue(
                "artifact_id",
                artifact.Id);

            var count = checked(
                (int)(long)(
                    await countCommand.ExecuteScalarAsync(
                        cancellationToken))!);
            var expectedCount = package.Segments.Count(
                segment => segment.ArtifactId == artifact.Id);

            if (count != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Artifact {artifact.Id} has {count} persisted segments; " +
                    $"profile {package.ProfileId} expects {expectedCount}.");
            }
        }
    }

    private static async Task ValidateArtifactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportArtifact expected,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT
                    a.manifestation_id,
                    a.derived_from_artifact_id,
                    a.artifact_type,
                    a.sha256,
                    a.media_type,
                    a.byte_length,
                    a.origin_uri,
                    a.lifecycle_status,
                    r.editorial_review_status
                FROM knowledge_artifacts a
                JOIN knowledge_resources r
                  ON r.id = a.id
                WHERE a.id = @id
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "id",
            expected.Id);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(cancellationToken) ||
            reader.GetGuid(0) != expected.ManifestationId ||
            !NullableGuidEquals(
                reader,
                1,
                expected.DerivedFromArtifactId) ||
            !string.Equals(
                reader.GetString(2),
                expected.ArtifactType,
                StringComparison.Ordinal) ||
            !string.Equals(
                reader.GetString(3).Trim(),
                expected.Sha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                reader.GetString(4),
                expected.MediaType,
                StringComparison.Ordinal) ||
            reader.GetInt64(5) != expected.ByteLength ||
            !NullableStringEquals(
                reader,
                6,
                expected.OriginUri) ||
            !string.Equals(
                reader.GetString(7),
                expected.LifecycleStatus,
                StringComparison.Ordinal) ||
            !string.Equals(
                reader.GetString(8),
                expected.EditorialReviewStatus,
                StringComparison.Ordinal) ||
            await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Persisted artifact {expected.Id} does not match the import package.");
        }
    }

    private static async Task ValidateSegmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportSegment expected,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT
                    s.artifact_id,
                    s.parent_segment_id,
                    s.segment_type,
                    s.segment_kind,
                    s.ordinal,
                    s.title,
                    s.text,
                    s.locator,
                    r.editorial_review_status
                FROM knowledge_document_segments s
                JOIN knowledge_resources r
                  ON r.id = s.id
                WHERE s.id = @id
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "id",
            expected.Id);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        if (!await reader.ReadAsync(cancellationToken) ||
            reader.GetGuid(0) != expected.ArtifactId ||
            !NullableGuidEquals(
                reader,
                1,
                expected.ParentSegmentId) ||
            !string.Equals(
                reader.GetString(2),
                ToDatabaseValue(expected.SegmentType),
                StringComparison.Ordinal) ||
            !string.Equals(
                reader.GetString(3),
                ToDatabaseValue(expected.SegmentKind),
                StringComparison.Ordinal) ||
            reader.GetInt32(4) != expected.Ordinal ||
            !NullableStringEquals(
                reader,
                5,
                expected.Title) ||
            !string.Equals(
                reader.GetString(6),
                expected.Text,
                StringComparison.Ordinal) ||
            !NullableStringEquals(
                reader,
                7,
                expected.Locator) ||
            !string.Equals(
                reader.GetString(8),
                expected.EditorialReviewStatus,
                StringComparison.Ordinal) ||
            await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Persisted segment {expected.Id} does not match the import package.");
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
                    to_regclass('public.knowledge_resources') IS NOT NULL
                    AND to_regclass('public.knowledge_works') IS NOT NULL
                    AND to_regclass('public.knowledge_expressions') IS NOT NULL
                    AND to_regclass('public.knowledge_expression_relations') IS NOT NULL
                    AND to_regclass('public.knowledge_manifestations') IS NOT NULL
                    AND to_regclass('public.knowledge_manifestation_identifiers') IS NOT NULL
                    AND to_regclass('public.knowledge_contributors') IS NOT NULL
                    AND to_regclass('public.knowledge_contributions') IS NOT NULL
                    AND to_regclass('public.knowledge_artifacts') IS NOT NULL
                    AND to_regclass('public.knowledge_processing_activities') IS NOT NULL
                    AND to_regclass('public.knowledge_document_segments') IS NOT NULL
                    AND to_regclass('public.knowledge_metadata_assertions') IS NOT NULL
                    AND to_regclass('public.knowledge_source_kinds') IS NOT NULL
                    AND to_regclass('public.knowledge_source_kind_assertions') IS NOT NULL
                    AND to_regclass('public.knowledge_perspectives') IS NOT NULL
                    AND to_regclass('public.knowledge_perspective_assertions') IS NOT NULL
                    AND to_regclass('public.knowledge_methodological_frameworks') IS NOT NULL
                    AND to_regclass('public.knowledge_methodological_framework_assertions') IS NOT NULL
                    AND to_regclass('public.knowledge_epistemic_frameworks') IS NOT NULL
                    AND to_regclass('public.knowledge_epistemic_framework_assertions') IS NOT NULL
                    AND to_regclass('public.knowledge_evidence_roles') IS NOT NULL
                    AND to_regclass('public.knowledge_evidence_role_assertions') IS NOT NULL
                """,
                connection);

        var valid =
            await command.ExecuteScalarAsync(cancellationToken);

        if (valid is not true)
        {
            throw new InvalidOperationException(
                "Knowledge Store schema is not ready. Apply Knowledge migrations before importing.");
        }
    }

    private static async Task AcquireImportLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string profileId,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtext(@key))",
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "key",
            "knowledge-import/" + profileId);

        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<bool> ResourceExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM knowledge_resources
                    WHERE id = @id)
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue("id", id);

        return (bool)(
            await command.ExecuteScalarAsync(
                cancellationToken))!;
    }

    private static async Task<bool> ArtifactHashExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sha256,
        CancellationToken cancellationToken)
    {
        await using var command =
            new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM knowledge_artifacts
                    WHERE sha256 = @sha256)
                """,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "sha256",
            sha256);

        return (bool)(
            await command.ExecuteScalarAsync(
                cancellationToken))!;
    }

    private static async Task InsertResourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string reviewStatus,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken) =>
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

    private static async Task EnsureContributorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportContributor contributor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using (var query =
                     new NpgsqlCommand(
                         """
                         SELECT contributor_type, preferred_name
                         FROM knowledge_contributors
                         WHERE id = @id
                         """,
                         connection,
                         transaction))
        {
            query.Parameters.AddWithValue(
                "id",
                contributor.Id);

            await using var reader =
                await query.ExecuteReaderAsync(
                    cancellationToken);

            if (await reader.ReadAsync(
                    cancellationToken))
            {
                var matches =
                    string.Equals(
                        reader.GetString(0),
                        contributor.ContributorType,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        reader.GetString(1),
                        contributor.PreferredName,
                        StringComparison.Ordinal) &&
                    !await reader.ReadAsync(
                        cancellationToken);

                if (!matches)
                {
                    throw new InvalidOperationException(
                        $"Contributor identity collision for {contributor.PreferredName}.");
                }

                return;
            }
        }

        await InsertResourceAsync(
            connection,
            transaction,
            contributor.Id,
            contributor.EditorialReviewStatus,
            now,
            cancellationToken);

        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", contributor.Id),
                ("contributor_type", contributor.ContributorType),
                ("preferred_name", contributor.PreferredName)
            };

        var sortNameSql =
            AddNullableParameter(
                parameters,
                "sort_name",
                contributor.SortName);

        var descriptionSql =
            AddNullableParameter(
                parameters,
                "description",
                contributor.Description);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_contributors
                 (id, contributor_type, preferred_name, sort_name, description)
             VALUES
                 (@id, @contributor_type, @preferred_name, {sortNameSql}, {descriptionSql})
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task InsertWorkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportWork work,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", work.Id),
                ("title", work.Title)
            };

        var languageSql =
            AddNullableParameter(
                parameters,
                "original_language",
                work.OriginalLanguage);

        var descriptionSql =
            AddNullableParameter(
                parameters,
                "description",
                work.Description);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_works
                 (id, title, original_language, description)
             VALUES
                 (@id, @title, {languageSql}, {descriptionSql})
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task InsertExpressionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportExpression expression,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", expression.Id),
                ("work_id", expression.WorkId),
                ("language_code", expression.LanguageCode)
            };

        var labelSql =
            AddNullableParameter(
                parameters,
                "label",
                expression.Label);

        var descriptionSql =
            AddNullableParameter(
                parameters,
                "description",
                expression.Description);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_expressions
                 (id, work_id, language_code, label, description)
             VALUES
                 (@id, @work_id, @language_code, {labelSql}, {descriptionSql})
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static Task InsertExpressionRelationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportExpressionRelation relation,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO knowledge_expression_relations
                (from_expression_id, to_expression_id, relation_type)
            VALUES
                (@from_id, @to_id, @relation_type)
            """,
            cancellationToken,
            ("from_id", relation.FromExpressionId),
            ("to_id", relation.ToExpressionId),
            ("relation_type", relation.RelationType));

    private static async Task InsertManifestationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportManifestation manifestation,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", manifestation.Id),
                ("expression_id", manifestation.ExpressionId)
            };

        var editionSql =
            AddNullableParameter(
                parameters,
                "edition_statement",
                manifestation.EditionStatement);

        var yearSql =
            AddNullableParameter(
                parameters,
                "publication_year",
                manifestation.PublicationYear);

        var placeSql =
            AddNullableParameter(
                parameters,
                "publication_place",
                manifestation.PublicationPlace);

        var citationSql =
            AddNullableParameter(
                parameters,
                "citation_label",
                manifestation.CitationLabel);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_manifestations
                 (id, expression_id, edition_statement, publication_year, publication_place, citation_label)
             VALUES
                 (@id, @expression_id, {editionSql}, {yearSql}, {placeSql}, {citationSql})
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task InsertManifestationIdentifierAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportManifestationIdentifier identifier,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("manifestation_id", identifier.ManifestationId),
                ("scheme", identifier.Scheme),
                ("value", identifier.Value)
            };

        var uriSql =
            AddNullableParameter(
                parameters,
                "uri",
                identifier.Uri);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_manifestation_identifiers
                 (manifestation_id, scheme, value, uri)
             VALUES
                 (@manifestation_id, @scheme, @value, {uriSql})
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task InsertArtifactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportArtifact artifact,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", artifact.Id),
                ("manifestation_id", artifact.ManifestationId),
                ("artifact_type", artifact.ArtifactType),
                ("sha256", artifact.Sha256),
                ("media_type", artifact.MediaType),
                ("byte_length", artifact.ByteLength),
                ("acquired_at", now),
                ("lifecycle_status", artifact.LifecycleStatus)
            };

        var derivedSql =
            AddNullableParameter(
                parameters,
                "derived_from_artifact_id",
                artifact.DerivedFromArtifactId);

        var originSql =
            AddNullableParameter(
                parameters,
                "origin_uri",
                artifact.OriginUri);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_artifacts
                 (id, manifestation_id, derived_from_artifact_id, artifact_type, sha256,
                  media_type, byte_length, origin_uri, acquired_at, lifecycle_status)
             VALUES
                 (@id, @manifestation_id, {derivedSql}, @artifact_type, @sha256,
                  @media_type, @byte_length, {originSql}, @acquired_at, @lifecycle_status)
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task InsertProcessingActivityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportProcessingActivity activity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("output_artifact_id", activity.OutputArtifactId),
                ("activity_type", activity.ActivityType),
                ("tool_name", activity.ToolName),
                ("tool_version", activity.ToolVersion),
                ("started_at", now),
                ("status", activity.Status)
            };

        var inputSql =
            AddNullableParameter(
                parameters,
                "input_artifact_id",
                activity.InputArtifactId);

        var configurationSql =
            AddNullableParameter(
                parameters,
                "configuration_json",
                activity.ConfigurationJson);

        var completedSql =
            string.Equals(
                activity.Status,
                "completed",
                StringComparison.Ordinal)
                ? AddNullableParameter(
                    parameters,
                    "completed_at",
                    now)
                : "NULL";

        var executedBySql =
            AddNullableParameter(
                parameters,
                "executed_by",
                activity.ExecutedBy);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_processing_activities
                 (input_artifact_id, output_artifact_id, activity_type, tool_name, tool_version,
                  configuration_json, started_at, completed_at, executed_by, status)
             VALUES
                 ({inputSql}, @output_artifact_id, @activity_type, @tool_name, @tool_version,
                  {configurationSql}::jsonb, @started_at, {completedSql}, {executedBySql}, @status)
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static Task InsertContributionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportContribution contribution,
        CancellationToken cancellationToken)
    {
        var targetSql =
            contribution.WorkId.HasValue
                ? "@target_id, NULL, NULL"
                : contribution.ExpressionId.HasValue
                    ? "NULL, @target_id, NULL"
                    : "NULL, NULL, @target_id";

        var targetId =
            contribution.WorkId ??
            contribution.ExpressionId ??
            contribution.ManifestationId!.Value;

        return ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_contributions
                 (contributor_id, work_id, expression_id, manifestation_id,
                  role, attribution_status, ordinal)
             VALUES
                 (@contributor_id, {targetSql},
                  @role, @attribution_status, @ordinal)
             """,
            cancellationToken,
            ("contributor_id", contribution.ContributorId),
            ("target_id", targetId),
            ("role", contribution.Role),
            ("attribution_status", contribution.AttributionStatus),
            ("ordinal", contribution.Ordinal));
    }

    private static async Task InsertSegmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportSegment segment,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", segment.Id),
                ("artifact_id", segment.ArtifactId),
                ("segment_type", ToDatabaseValue(segment.SegmentType)),
                ("segment_kind", ToDatabaseValue(segment.SegmentKind)),
                ("ordinal", segment.Ordinal),
                ("text", segment.Text)
            };

        var parentSql =
            AddNullableParameter(
                parameters,
                "parent_segment_id",
                segment.ParentSegmentId);

        var titleSql =
            AddNullableParameter(
                parameters,
                "title",
                segment.Title);

        var locatorSql =
            AddNullableParameter(
                parameters,
                "locator",
                segment.Locator);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_document_segments
                 (id, artifact_id, parent_segment_id, segment_type, segment_kind,
                  ordinal, title, text, locator)
             VALUES
                 (@id, @artifact_id, {parentSql}, @segment_type, @segment_kind,
                  @ordinal, {titleSql}, @text, {locatorSql})
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task<
        IReadOnlyDictionary<
            (KnowledgeClassificationDimension Dimension, string Code),
            Guid>>
        EnsureClassificationTermsAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IReadOnlyList<KnowledgeImportClassificationTerm> terms,
            CancellationToken cancellationToken)
    {
        var result =
            new Dictionary<
                (KnowledgeClassificationDimension Dimension, string Code),
                Guid>();

        foreach (var term in terms)
        {
            var id = await EnsureClassificationTermAsync(
                connection,
                transaction,
                term,
                cancellationToken);

            result.Add(
                (term.Dimension, term.Code),
                id);
        }

        return result;
    }

    private static async Task<Guid> EnsureClassificationTermAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportClassificationTerm term,
        CancellationToken cancellationToken)
    {
        if (term.Dimension ==
            KnowledgeClassificationDimension.Perspective)
        {
            return await EnsurePerspectiveAsync(
                connection,
                transaction,
                term,
                cancellationToken);
        }

        var mapping =
            GetClassificationMapping(term.Dimension);

        await using (var query =
                     new NpgsqlCommand(
                         $"SELECT id FROM {mapping.TermTable} WHERE code = @code",
                         connection,
                         transaction))
        {
            query.Parameters.AddWithValue(
                "code",
                term.Code);

            var existing =
                await query.ExecuteScalarAsync(
                    cancellationToken);

            if (existing is Guid existingId)
            {
                return existingId;
            }
        }

        var id =
            KnowledgeStableIds.ForVocabulary(
                mapping.VocabularyPrefix +
                term.Code);

        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", id),
                ("code", term.Code),
                ("label", term.Label)
            };

        var descriptionSql =
            AddNullableParameter(
                parameters,
                "description",
                term.Description);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO {mapping.TermTable}
                 (id, code, label, description)
             VALUES
                 (@id, @code, @label, {descriptionSql})
             """,
            cancellationToken,
            parameters.ToArray());

        return id;
    }

    private static async Task<Guid> EnsurePerspectiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportClassificationTerm term,
        CancellationToken cancellationToken)
    {
        await using (var query =
                     new NpgsqlCommand(
                         """
                         SELECT id
                         FROM knowledge_perspectives
                         WHERE code = @code
                         """,
                         connection,
                         transaction))
        {
            query.Parameters.AddWithValue(
                "code",
                term.Code);

            var existing =
                await query.ExecuteScalarAsync(
                    cancellationToken);

            if (existing is Guid existingId)
            {
                return existingId;
            }
        }

        var id =
            KnowledgeStableIds.ForVocabulary(
                "perspective:" + term.Code);

        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", id),
                ("code", term.Code),
                ("label", term.Label)
            };

        var descriptionSql =
            AddNullableParameter(
                parameters,
                "description",
                term.Description);

        var periodSql =
            AddNullableParameter(
                parameters,
                "historical_period",
                term.HistoricalPeriod);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_perspectives
                 (id, code, label, parent_perspective_id, description, historical_period)
             VALUES
                 (@id, @code, @label, NULL, {descriptionSql}, {periodSql})
             """,
            cancellationToken,
            parameters.ToArray());

        return id;
    }

    private static async Task InsertClassificationAssertionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportClassificationAssertion assertion,
        Guid termId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mapping =
            GetClassificationMapping(
                assertion.Dimension);

        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", assertion.Id),
                ("resource_id", assertion.ResourceId),
                ("term_id", termId),
                ("assertion_origin", assertion.AssertionOrigin),
                ("asserted_by", assertion.AssertedBy),
                ("asserted_at", now),
                ("review_status", assertion.ReviewStatus)
            };

        var reviewedBySql =
            AddNullableParameter(
                parameters,
                "reviewed_by",
                assertion.ReviewedBy);

        var reviewedAtSql =
            assertion.ReviewedBy is null
                ? "NULL"
                : AddNullableParameter(
                    parameters,
                    "reviewed_at",
                    now);

        var justificationSql =
            AddNullableParameter(
                parameters,
                "justification",
                assertion.Justification);

        var supportingSql =
            AddNullableParameter(
                parameters,
                "supporting_segment_id",
                assertion.SupportingSegmentId);

        var supersedesSql =
            AddNullableParameter(
                parameters,
                "supersedes_assertion_id",
                assertion.SupersedesAssertionId);

        if (mapping.TypeColumn is null)
        {
            await ExecuteAsync(
                connection,
                transaction,
                $"""
                 INSERT INTO {mapping.AssertionTable}
                     (id, resource_id, {mapping.TermForeignKey},
                      assertion_origin, asserted_by, asserted_at,
                      review_status, reviewed_by, reviewed_at,
                      justification, supporting_segment_id, supersedes_assertion_id)
                 VALUES
                     (@id, @resource_id, @term_id,
                      @assertion_origin, @asserted_by, @asserted_at,
                      @review_status, {reviewedBySql}, {reviewedAtSql},
                      {justificationSql}, {supportingSql}, {supersedesSql})
                 """,
                cancellationToken,
                parameters.ToArray());

            return;
        }

        parameters.Add(
            ("classification_type",
             assertion.ClassificationType!));

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO {mapping.AssertionTable}
                 (id, resource_id, {mapping.TermForeignKey}, {mapping.TypeColumn},
                  assertion_origin, asserted_by, asserted_at,
                  review_status, reviewed_by, reviewed_at,
                  justification, supporting_segment_id, supersedes_assertion_id)
             VALUES
                 (@id, @resource_id, @term_id, @classification_type,
                  @assertion_origin, @asserted_by, @asserted_at,
                  @review_status, {reviewedBySql}, {reviewedAtSql},
                  {justificationSql}, {supportingSql}, {supersedesSql})
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task InsertMetadataAssertionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportMetadataAssertion assertion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("id", assertion.Id),
                ("resource_id", assertion.ResourceId),
                ("property", assertion.Property),
                ("value", assertion.Value),
                ("assertion_origin", assertion.AssertionOrigin),
                ("asserted_by", assertion.AssertedBy),
                ("asserted_at", now),
                ("review_status", assertion.ReviewStatus)
            };

        var reviewedBySql =
            AddNullableParameter(
                parameters,
                "reviewed_by",
                assertion.ReviewedBy);

        var reviewedAtSql =
            assertion.ReviewedBy is null
                ? "NULL"
                : AddNullableParameter(
                    parameters,
                    "reviewed_at",
                    now);

        var confidenceSql =
            AddNullableParameter(
                parameters,
                "confidence",
                assertion.Confidence);

        var justificationSql =
            AddNullableParameter(
                parameters,
                "justification",
                assertion.Justification);

        var supportingSql =
            AddNullableParameter(
                parameters,
                "supporting_segment_id",
                assertion.SupportingSegmentId);

        var supersedesSql =
            AddNullableParameter(
                parameters,
                "supersedes_assertion_id",
                assertion.SupersedesAssertionId);

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             INSERT INTO knowledge_metadata_assertions
                 (id, resource_id, property, value,
                  assertion_origin, asserted_by, asserted_at,
                  review_status, reviewed_by, reviewed_at,
                  confidence, justification,
                  supporting_segment_id, supersedes_assertion_id)
             VALUES
                 (@id, @resource_id, @property, @value,
                  @assertion_origin, @asserted_by, @asserted_at,
                  @review_status, {reviewedBySql}, {reviewedAtSql},
                  {confidenceSql}, {justificationSql},
                  {supportingSql}, {supersedesSql})
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static Task DeleteClassificationAssertionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportClassificationAssertion assertion,
        CancellationToken cancellationToken)
    {
        var mapping =
            GetClassificationMapping(
                assertion.Dimension);

        return ExecuteAsync(
            connection,
            transaction,
            $"DELETE FROM {mapping.AssertionTable} WHERE id = @id",
            cancellationToken,
            ("id", assertion.Id));
    }

    private static Task DeleteContributionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportContribution contribution,
        CancellationToken cancellationToken)
    {
        string targetPredicate;
        Guid targetId;

        if (contribution.WorkId is Guid workId)
        {
            targetPredicate =
                "work_id = @target_id AND expression_id IS NULL AND manifestation_id IS NULL";
            targetId = workId;
        }
        else if (contribution.ExpressionId is Guid expressionId)
        {
            targetPredicate =
                "work_id IS NULL AND expression_id = @target_id AND manifestation_id IS NULL";
            targetId = expressionId;
        }
        else
        {
            targetPredicate =
                "work_id IS NULL AND expression_id IS NULL AND manifestation_id = @target_id";
            targetId =
                contribution.ManifestationId!.Value;
        }

        return ExecuteAsync(
            connection,
            transaction,
            $"""
             DELETE FROM knowledge_contributions
             WHERE contributor_id = @contributor_id
               AND {targetPredicate}
               AND role = @role
               AND attribution_status = @attribution_status
               AND ordinal = @ordinal
             """,
            cancellationToken,
            ("contributor_id", contribution.ContributorId),
            ("target_id", targetId),
            ("role", contribution.Role),
            ("attribution_status", contribution.AttributionStatus),
            ("ordinal", contribution.Ordinal));
    }

    private static async Task DeleteManifestationIdentifierAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        KnowledgeImportManifestationIdentifier identifier,
        CancellationToken cancellationToken)
    {
        var parameters =
            new List<(string Name, object Value)>
            {
                ("manifestation_id", identifier.ManifestationId),
                ("scheme", identifier.Scheme),
                ("value", identifier.Value)
            };

        var uriPredicate =
            identifier.Uri is null
                ? "uri IS NULL"
                : "uri = @uri";

        if (identifier.Uri is not null)
        {
            parameters.Add(("uri", identifier.Uri));
        }

        await ExecuteAsync(
            connection,
            transaction,
            $"""
             DELETE FROM knowledge_manifestation_identifiers
             WHERE manifestation_id = @manifestation_id
               AND scheme = @scheme
               AND value = @value
               AND {uriPredicate}
             """,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task DeleteContributorIfUnusedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid contributorId,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM knowledge_contributors c
            WHERE c.id = @id
              AND NOT EXISTS (
                  SELECT 1
                  FROM knowledge_contributions contribution
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
                  SELECT 1
                  FROM knowledge_contributors c
                  WHERE c.id = r.id)
            """,
            cancellationToken,
            ("id", contributorId));
    }

    private static async Task DeleteUnusedClassificationTermsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<KnowledgeImportClassificationTerm> terms,
        CancellationToken cancellationToken)
    {
        foreach (var term in terms.Reverse())
        {
            var mapping =
                GetClassificationMapping(term.Dimension);

            var childPredicate =
                term.Dimension ==
                    KnowledgeClassificationDimension.Perspective
                    ? """
                      AND NOT EXISTS (
                          SELECT 1
                          FROM knowledge_perspectives child
                          WHERE child.parent_perspective_id = term.id)
                      """
                    : string.Empty;

            await ExecuteAsync(
                connection,
                transaction,
                $"""
                 DELETE FROM {mapping.TermTable} term
                 WHERE term.code = @code
                   AND NOT EXISTS (
                       SELECT 1
                       FROM {mapping.AssertionTable} assertion
                       WHERE assertion.{mapping.TermForeignKey} = term.id)
                   {childPredicate}
                 """,
                cancellationToken,
                ("code", term.Code));
        }
    }

    private static ClassificationMapping GetClassificationMapping(
        KnowledgeClassificationDimension dimension) =>
        dimension switch
        {
            KnowledgeClassificationDimension.SourceKind =>
                new(
                    "knowledge_source_kinds",
                    "knowledge_source_kind_assertions",
                    "source_kind_id",
                    null,
                    "source-kind:"),
            KnowledgeClassificationDimension.Perspective =>
                new(
                    "knowledge_perspectives",
                    "knowledge_perspective_assertions",
                    "perspective_id",
                    "perspective_type",
                    "perspective:"),
            KnowledgeClassificationDimension.MethodologicalFramework =>
                new(
                    "knowledge_methodological_frameworks",
                    "knowledge_methodological_framework_assertions",
                    "methodological_framework_id",
                    "classification_type",
                    "methodological-framework:"),
            KnowledgeClassificationDimension.EpistemicFramework =>
                new(
                    "knowledge_epistemic_frameworks",
                    "knowledge_epistemic_framework_assertions",
                    "epistemic_framework_id",
                    "classification_type",
                    "epistemic-framework:"),
            KnowledgeClassificationDimension.EvidenceRole =>
                new(
                    "knowledge_evidence_roles",
                    "knowledge_evidence_role_assertions",
                    "evidence_role_id",
                    null,
                    "evidence-role:"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(dimension),
                dimension,
                "Unsupported classification dimension.")
        };

    private static string AddNullableParameter(
        ICollection<(string Name, object Value)> parameters,
        string name,
        object? value)
    {
        if (value is null)
        {
            return "NULL";
        }

        parameters.Add((name, value));
        return "@" + name;
    }

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

    private static bool NullableGuidEquals(
        NpgsqlDataReader reader,
        int ordinal,
        Guid? expected) =>
        reader.IsDBNull(ordinal)
            ? expected is null
            : expected.HasValue &&
              reader.GetGuid(ordinal) == expected.Value;

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

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record OwnedResource(
        Guid Id,
        string ReviewStatus);

    private sealed record ClassificationMapping(
        string TermTable,
        string AssertionTable,
        string TermForeignKey,
        string? TypeColumn,
        string VocabularyPrefix);
}
