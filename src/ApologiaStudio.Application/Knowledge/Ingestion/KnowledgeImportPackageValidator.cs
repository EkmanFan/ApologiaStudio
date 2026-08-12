using System.Security.Cryptography;
using System.Text.Json;

namespace ApologiaStudio.Application.Knowledge.Ingestion;

public static class KnowledgeImportPackageValidator
{
    public static void Validate(KnowledgeImportPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        RequireText(package.ProfileId, nameof(package.ProfileId));
        RequireText(package.StableIdNamespace, nameof(package.StableIdNamespace));
        RequireText(package.EditorialActor, nameof(package.EditorialActor));

        if (package.StableIdNamespace.IndexOfAny(['/', '\\']) >= 0)
        {
            throw Invalid(
                "StableIdNamespace must be a single safe namespace component.");
        }

        var resourceIds = new HashSet<Guid>();

        AddResources(
            package.Works.Select(item => (item.Id, item.EditorialReviewStatus)),
            "work",
            resourceIds);
        AddResources(
            package.Expressions.Select(item => (item.Id, item.EditorialReviewStatus)),
            "expression",
            resourceIds);
        AddResources(
            package.Manifestations.Select(item => (item.Id, item.EditorialReviewStatus)),
            "manifestation",
            resourceIds);
        AddResources(
            package.Artifacts.Select(item => (item.Id, item.EditorialReviewStatus)),
            "artifact",
            resourceIds);
        AddResources(
            package.Segments.Select(item => (item.Id, item.EditorialReviewStatus)),
            "segment",
            resourceIds);

        var contributorIds = new HashSet<Guid>();
        foreach (var contributor in package.Contributors)
        {
            if (!contributorIds.Add(contributor.Id))
            {
                throw Invalid(
                    $"Duplicate contributor id {contributor.Id}.");
            }

            if (resourceIds.Contains(contributor.Id))
            {
                throw Invalid(
                    $"Contributor id {contributor.Id} collides with a package-owned resource.");
            }

            RequireOneOf(
                contributor.EditorialReviewStatus,
                "Contributor EditorialReviewStatus",
                "pending",
                "in_review",
                "approved",
                "rejected");
            RequireOneOf(
                contributor.ContributorType,
                "ContributorType",
                "person",
                "collective_body");
            RequireText(
                contributor.PreferredName,
                "Contributor PreferredName");
        }

        var allResourceIds = new HashSet<Guid>(resourceIds);
        allResourceIds.UnionWith(contributorIds);

        if (!package.Works.Any(work => work.Id == package.PrimaryWorkId))
        {
            throw Invalid(
                "PrimaryWorkId does not identify a work in the package.");
        }

        var normalizedArtifact = package.Artifacts.SingleOrDefault(
            artifact => artifact.Id == package.NormalizedArtifactId);

        if (normalizedArtifact is null)
        {
            throw Invalid(
                "NormalizedArtifactId does not identify an artifact in the package.");
        }

        if (!string.Equals(
                normalizedArtifact.ArtifactType,
                "normalized",
                StringComparison.Ordinal))
        {
            throw Invalid(
                "NormalizedArtifactId must identify an artifact whose type is 'normalized'.");
        }

        var workIds = package.Works
            .Select(item => item.Id)
            .ToHashSet();
        var expressionIds = package.Expressions
            .Select(item => item.Id)
            .ToHashSet();
        var manifestationIds = package.Manifestations
            .Select(item => item.Id)
            .ToHashSet();
        var artifactIds = package.Artifacts
            .Select(item => item.Id)
            .ToHashSet();
        var segmentIds = package.Segments
            .Select(item => item.Id)
            .ToHashSet();

        foreach (var work in package.Works)
        {
            RequireText(work.Title, "Work title");
        }

        foreach (var expression in package.Expressions)
        {
            if (!workIds.Contains(expression.WorkId))
            {
                throw Invalid(
                    $"Expression {expression.Id} references unknown work {expression.WorkId}.");
            }

            RequireText(
                expression.LanguageCode,
                "Expression LanguageCode");
        }

        foreach (var relation in package.ExpressionRelations)
        {
            if (!expressionIds.Contains(relation.FromExpressionId) ||
                !expressionIds.Contains(relation.ToExpressionId))
            {
                throw Invalid(
                    "Expression relation references an expression outside the package.");
            }

            if (relation.FromExpressionId == relation.ToExpressionId)
            {
                throw Invalid(
                    "An expression relation cannot reference the same expression on both sides.");
            }

            RequireOneOf(
                relation.RelationType,
                "Expression relation type",
                "translation_of",
                "revision_of",
                "adaptation_of",
                "derived_from");
        }

        foreach (var manifestation in package.Manifestations)
        {
            if (!expressionIds.Contains(manifestation.ExpressionId))
            {
                throw Invalid(
                    $"Manifestation {manifestation.Id} references unknown expression {manifestation.ExpressionId}.");
            }

            if (manifestation.PublicationYear is int year &&
                year is < 1 or > 9999)
            {
                throw Invalid(
                    $"Manifestation {manifestation.Id} has invalid publication year {year}.");
            }
        }

        foreach (var identifier in package.ManifestationIdentifiers)
        {
            if (!manifestationIds.Contains(identifier.ManifestationId))
            {
                throw Invalid(
                    "Manifestation identifier references a manifestation outside the package.");
            }

            RequireText(identifier.Scheme, "Identifier scheme");
            RequireText(identifier.Value, "Identifier value");
        }

        foreach (var contribution in package.Contributions)
        {
            if (!contributorIds.Contains(contribution.ContributorId))
            {
                throw Invalid(
                    $"Contribution references unknown contributor {contribution.ContributorId}.");
            }

            var targetCount =
                (contribution.WorkId.HasValue ? 1 : 0) +
                (contribution.ExpressionId.HasValue ? 1 : 0) +
                (contribution.ManifestationId.HasValue ? 1 : 0);

            if (targetCount != 1)
            {
                throw Invalid(
                    "A contribution must target exactly one Work, Expression, or Manifestation.");
            }

            if (contribution.WorkId is Guid workId &&
                !workIds.Contains(workId))
            {
                throw Invalid(
                    $"Contribution references unknown work {workId}.");
            }

            if (contribution.ExpressionId is Guid expressionId &&
                !expressionIds.Contains(expressionId))
            {
                throw Invalid(
                    $"Contribution references unknown expression {expressionId}.");
            }

            if (contribution.ManifestationId is Guid manifestationId &&
                !manifestationIds.Contains(manifestationId))
            {
                throw Invalid(
                    $"Contribution references unknown manifestation {manifestationId}.");
            }

            RequireOneOf(
                contribution.Role,
                "Contribution role",
                "author",
                "corporate_author",
                "compiler",
                "issuing_body",
                "translator",
                "reviser",
                "textual_editor",
                "transcriber",
                "commentator",
                "publisher",
                "series_editor",
                "distributor",
                "producer");
            RequireOneOf(
                contribution.AttributionStatus,
                "Contribution AttributionStatus",
                "explicit",
                "established",
                "traditional",
                "probable",
                "possible",
                "disputed");

            if (contribution.Ordinal < 0)
            {
                throw Invalid(
                    "Contribution ordinal must be non-negative.");
            }
        }

        var artifactsSeen = new HashSet<Guid>();
        foreach (var artifact in package.Artifacts)
        {
            if (!manifestationIds.Contains(artifact.ManifestationId))
            {
                throw Invalid(
                    $"Artifact {artifact.Id} references unknown manifestation {artifact.ManifestationId}.");
            }

            if (artifact.DerivedFromArtifactId is Guid parentId &&
                !artifactsSeen.Contains(parentId))
            {
                throw Invalid(
                    $"Artifact {artifact.Id} must appear after its derived-from artifact {parentId}.");
            }

            RequireOneOf(
                artifact.ArtifactType,
                "ArtifactType",
                "raw",
                "ocr",
                "parsed",
                "normalized");
            RequireSafeStorageComponent(
                artifact.ArtifactType,
                "ArtifactType");
            RequireSha256(artifact.Sha256, "Artifact SHA-256");
            RequireText(artifact.MediaType, "Artifact MediaType");
            RequireOneOf(
                artifact.LifecycleStatus,
                "Artifact LifecycleStatus",
                "active",
                "superseded",
                "retired",
                "corrupted",
                "deleted");

            if (artifact.ByteLength <= 0)
            {
                throw Invalid(
                    $"Artifact {artifact.Id} must have a positive ByteLength.");
            }

            if (string.IsNullOrWhiteSpace(artifact.FileExtension) ||
                !artifact.FileExtension.StartsWith(
                    ".",
                    StringComparison.Ordinal) ||
                artifact.FileExtension.IndexOfAny(
                    ['/', '\\']) >= 0)
            {
                throw Invalid(
                    $"Artifact {artifact.Id} has an invalid FileExtension.");
            }

            var hasSourcePath =
                !string.IsNullOrWhiteSpace(artifact.SourcePath);
            var hasBytes = artifact.Bytes is not null;

            if (hasSourcePath == hasBytes)
            {
                throw Invalid(
                    $"Artifact {artifact.Id} must provide exactly one payload source: SourcePath or Bytes.");
            }

            if (artifact.Bytes is { } bytes)
            {
                if (bytes.LongLength != artifact.ByteLength)
                {
                    throw Invalid(
                        $"Artifact {artifact.Id} byte payload length does not match ByteLength.");
                }

                var actualSha256 = Convert
                    .ToHexString(SHA256.HashData(bytes))
                    .ToLowerInvariant();

                if (!string.Equals(
                        actualSha256,
                        artifact.Sha256,
                        StringComparison.Ordinal))
                {
                    throw Invalid(
                        $"Artifact {artifact.Id} byte payload does not match its SHA-256 identity.");
                }
            }

            artifactsSeen.Add(artifact.Id);
        }

        var processingOutputs = new HashSet<Guid>();
        foreach (var activity in package.ProcessingActivities)
        {
            if (activity.InputArtifactId is Guid inputId)
            {
                if (!artifactIds.Contains(inputId))
                {
                    throw Invalid(
                        $"Processing activity references unknown input artifact {inputId}.");
                }

                if (inputId == activity.OutputArtifactId)
                {
                    throw Invalid(
                        "A processing activity cannot use the same artifact as input and output.");
                }
            }

            if (!artifactIds.Contains(activity.OutputArtifactId))
            {
                throw Invalid(
                    $"Processing activity references unknown output artifact {activity.OutputArtifactId}.");
            }

            if (!processingOutputs.Add(activity.OutputArtifactId))
            {
                throw Invalid(
                    $"Multiple processing activities target output artifact {activity.OutputArtifactId}.");
            }

            RequireOneOf(
                activity.ActivityType,
                "Processing ActivityType",
                "download",
                "ocr",
                "parse",
                "normalize",
                "correct");
            RequireText(activity.ToolName, "Processing ToolName");
            RequireText(activity.ToolVersion, "Processing ToolVersion");
            RequireOneOf(
                activity.Status,
                "Processing Status",
                "pending",
                "completed",
                "failed");

            if (!string.IsNullOrWhiteSpace(activity.ConfigurationJson))
            {
                try
                {
                    using var _ = JsonDocument.Parse(
                        activity.ConfigurationJson);
                }
                catch (JsonException exception)
                {
                    throw Invalid(
                        $"Processing configuration is not valid JSON: {exception.Message}");
                }
            }
        }

        var segmentOrdinals = new HashSet<(Guid ArtifactId, int Ordinal)>();
        var segmentsSeen = new HashSet<Guid>();

        foreach (var segment in package.Segments)
        {
            if (!artifactIds.Contains(segment.ArtifactId))
            {
                throw Invalid(
                    $"Segment {segment.Id} references unknown artifact {segment.ArtifactId}.");
            }

            if (segment.ParentSegmentId is Guid parentId)
            {
                if (!segmentsSeen.Contains(parentId))
                {
                    throw Invalid(
                        $"Segment {segment.Id} must appear after parent segment {parentId}.");
                }

                var parent = package.Segments.First(item =>
                    item.Id == parentId);
                if (parent.ArtifactId != segment.ArtifactId)
                {
                    throw Invalid(
                        $"Segment {segment.Id} and its parent must belong to the same artifact.");
                }
            }

            if (segment.Ordinal < 0)
            {
                throw Invalid(
                    $"Segment {segment.Id} ordinal must be non-negative.");
            }

            if (!segmentOrdinals.Add(
                    (segment.ArtifactId, segment.Ordinal)))
            {
                throw Invalid(
                    $"Duplicate segment ordinal {segment.Ordinal} for artifact {segment.ArtifactId}.");
            }

            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                throw Invalid(
                    $"Segment {segment.Id} has empty text.");
            }

            segmentsSeen.Add(segment.Id);
        }

        var termKeys = new HashSet<
            (KnowledgeClassificationDimension Dimension, string Code)>();

        foreach (var term in package.ClassificationTerms)
        {
            RequireText(term.Code, "Classification term code");
            RequireText(term.Label, "Classification term label");

            if (!termKeys.Add((term.Dimension, term.Code)))
            {
                throw Invalid(
                    $"Duplicate classification term {term.Dimension}/{term.Code}.");
            }
        }

        var assertionIds = new HashSet<Guid>();
        foreach (var assertion in package.ClassificationAssertions)
        {
            if (!assertionIds.Add(assertion.Id))
            {
                throw Invalid(
                    $"Duplicate classification assertion id {assertion.Id}.");
            }

            if (!allResourceIds.Contains(assertion.ResourceId))
            {
                throw Invalid(
                    $"Classification assertion {assertion.Id} references unknown resource {assertion.ResourceId}.");
            }

            if (!termKeys.Contains(
                    (assertion.Dimension, assertion.TermCode)))
            {
                throw Invalid(
                    $"Classification assertion {assertion.Id} references unknown term {assertion.Dimension}/{assertion.TermCode}.");
            }

            if (RequiresClassificationType(assertion.Dimension))
            {
                RequireOneOf(
                    assertion.ClassificationType,
                    "ClassificationType",
                    "declared",
                    "analytical");
            }
            else if (!string.IsNullOrWhiteSpace(
                         assertion.ClassificationType))
            {
                throw Invalid(
                    $"ClassificationType is not valid for dimension {assertion.Dimension}.");
            }

            RequireOneOf(
                assertion.AssertionOrigin,
                "Classification AssertionOrigin",
                "imported",
                "ai_proposed",
                "editorial");
            RequireText(
                assertion.AssertedBy,
                "Classification AssertedBy");
            RequireOneOf(
                assertion.ReviewStatus,
                "Classification ReviewStatus",
                "proposed",
                "verified",
                "rejected",
                "disputed",
                "superseded");

            if (assertion.SupersedesAssertionId == assertion.Id)
            {
                throw Invalid(
                    $"Classification assertion {assertion.Id} cannot supersede itself.");
            }

            if (assertion.SupportingSegmentId is Guid supportingId &&
                !segmentIds.Contains(supportingId))
            {
                throw Invalid(
                    $"Classification assertion {assertion.Id} references unknown supporting segment {supportingId}.");
            }
        }

        var metadataAssertionIds = new HashSet<Guid>();
        foreach (var assertion in package.MetadataAssertions)
        {
            if (!metadataAssertionIds.Add(assertion.Id))
            {
                throw Invalid(
                    $"Duplicate metadata assertion id {assertion.Id}.");
            }

            if (!allResourceIds.Contains(assertion.ResourceId))
            {
                throw Invalid(
                    $"Metadata assertion {assertion.Id} references unknown resource {assertion.ResourceId}.");
            }

            RequireText(assertion.Property, "Metadata Property");
            RequireText(assertion.Value, "Metadata Value");
            RequireOneOf(
                assertion.AssertionOrigin,
                "Metadata AssertionOrigin",
                "imported",
                "ai_proposed",
                "editorial");
            RequireText(
                assertion.AssertedBy,
                "Metadata AssertedBy");
            RequireOneOf(
                assertion.ReviewStatus,
                "Metadata ReviewStatus",
                "proposed",
                "verified",
                "rejected",
                "disputed",
                "superseded");

            if (assertion.SupersedesAssertionId == assertion.Id)
            {
                throw Invalid(
                    $"Metadata assertion {assertion.Id} cannot supersede itself.");
            }

            if (assertion.Confidence is double confidence &&
                (!double.IsFinite(confidence) ||
                 confidence is < 0 or > 1))
            {
                throw Invalid(
                    $"Metadata assertion {assertion.Id} has invalid confidence {confidence}.");
            }

            if (assertion.SupportingSegmentId is Guid supportingId &&
                !segmentIds.Contains(supportingId))
            {
                throw Invalid(
                    $"Metadata assertion {assertion.Id} references unknown supporting segment {supportingId}.");
            }
        }
    }

    private static void AddResources(
        IEnumerable<(Guid Id, string ReviewStatus)> items,
        string kind,
        ISet<Guid> resourceIds)
    {
        foreach (var (id, reviewStatus) in items)
        {
            if (!resourceIds.Add(id))
            {
                throw Invalid(
                    $"Duplicate package-owned resource id {id} ({kind}).");
            }

            RequireOneOf(
                reviewStatus,
                $"{kind} EditorialReviewStatus",
                "pending",
                "in_review",
                "approved",
                "rejected");
        }
    }

    private static bool RequiresClassificationType(
        KnowledgeClassificationDimension dimension) =>
        dimension is
            KnowledgeClassificationDimension.Perspective or
            KnowledgeClassificationDimension.MethodologicalFramework or
            KnowledgeClassificationDimension.EpistemicFramework;

    private static void RequireSafeStorageComponent(
        string value,
        string name)
    {
        if (value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '_' or '-')))
        {
            throw Invalid(
                $"{name} contains characters that are not safe for managed artifact paths.");
        }
    }

    private static void RequireSha256(
        string value,
        string name)
    {
        if (value.Length != 64 ||
            value.Any(character =>
                character is not (
                    >= '0' and <= '9' or
                    >= 'a' and <= 'f')))
        {
            throw Invalid(
                $"{name} must be a lowercase SHA-256 value.");
        }
    }

    private static void RequireOneOf(
        string? value,
        string name,
        params string[] allowedValues)
    {
        RequireText(value, name);

        if (!allowedValues.Contains(
                value!,
                StringComparer.Ordinal))
        {
            throw Invalid(
                $"{name} has unsupported value '{value}'.");
        }
    }

    private static void RequireText(
        string? value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid($"{name} must be defined.");
        }
    }

    private static KnowledgeImportPackageValidationException Invalid(
        string message) =>
        new(message);
}

public sealed class KnowledgeImportPackageValidationException(
    string message)
    : InvalidOperationException(message);
