using System.Globalization;
using ApologiaStudio.Application.Knowledge.Ingestion;

namespace ApologiaStudio.KnowledgeImporter;

internal static class DeDecretisImportPackageFactory
{
    private const string StableIdNamespace =
        "de-decretis-npnf2-04";

    private const string EditorialActor =
        "ApologiaStudio curated source manifest";

    private static readonly Guid VolumeWorkId =
        StableKnowledgeIds.ForProfile("volume-work");

    private static readonly Guid VolumeExpressionId =
        StableKnowledgeIds.ForProfile("volume-expression");

    private static readonly Guid VolumeManifestationId =
        StableKnowledgeIds.ForProfile("volume-manifestation");

    private static readonly Guid RawArtifactId =
        StableKnowledgeIds.ForProfile("raw-artifact");

    private static readonly Guid WorkId =
        StableKnowledgeIds.ForProfile("de-decretis-work");

    private static readonly Guid GreekExpressionId =
        StableKnowledgeIds.ForProfile("de-decretis-expression-grc");

    private static readonly Guid EnglishExpressionId =
        StableKnowledgeIds.ForProfile("de-decretis-expression-en");

    private static readonly Guid ManifestationId =
        StableKnowledgeIds.ForProfile("de-decretis-manifestation");

    private static readonly Guid ParsedArtifactId =
        StableKnowledgeIds.ForProfile("parsed-artifact");

    private static readonly Guid NormalizedArtifactId =
        StableKnowledgeIds.ForProfile("normalized-artifact");

    private static readonly Guid AthanasiusId =
        StableKnowledgeIds.ForAuthority(
            "person:athanasius-of-alexandria");

    private static readonly Guid NewmanId =
        StableKnowledgeIds.ForAuthority(
            "person:john-henry-newman");

    private static readonly Guid RobertsonId =
        StableKnowledgeIds.ForAuthority(
            "person:archibald-robertson");

    public static KnowledgeImportPackage Create(
        PreparedDeDecretis prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);

        var package = new KnowledgeImportPackage(
            DeDecretisDocument.ProfileId,
            StableIdNamespace,
            WorkId,
            NormalizedArtifactId,
            EditorialActor,
            CreateWorks(),
            CreateExpressions(),
            CreateExpressionRelations(),
            CreateManifestations(),
            CreateManifestationIdentifiers(),
            CreateContributors(),
            CreateContributions(),
            CreateArtifacts(prepared),
            CreateProcessingActivities(),
            CreateSegments(prepared),
            CreateClassificationTerms(),
            CreateClassificationAssertions(),
            CreateMetadataAssertions());

        KnowledgeImportPackageValidator.Validate(package);
        return package;
    }

    private static IReadOnlyList<KnowledgeImportWork>
        CreateWorks() =>
    [
        new(
            VolumeWorkId,
            "approved",
            "NPNF2-04: Athanasius — Select Works and Letters",
            null,
            "Editorial compilation represented by the complete CCEL PDF artifact."),
        new(
            WorkId,
            "approved",
            "De Decretis (Defence of the Nicene Definition)",
            "grc",
            "Athanasius's defence and explanation of the Nicene definition.")
    ];

    private static IReadOnlyList<KnowledgeImportExpression>
        CreateExpressions() =>
    [
        new(
            VolumeExpressionId,
            "approved",
            VolumeWorkId,
            "en",
            "English editorial compilation edited by Archibald Robertson",
            "English NPNF editorial expression containing selected works and letters of Athanasius."),
        new(
            GreekExpressionId,
            "approved",
            WorkId,
            "grc",
            "Original Greek expression",
            "Bibliographic expression record only; no Greek artifact is ingested in 6D."),
        new(
            EnglishExpressionId,
            "approved",
            WorkId,
            "en",
            "NPNF English translation/revision",
            "English translation represented in NPNF2-04, based on Newman's earlier translation and revised for this volume.")
    ];

    private static IReadOnlyList<KnowledgeImportExpressionRelation>
        CreateExpressionRelations() =>
    [
        new(
            EnglishExpressionId,
            GreekExpressionId,
            "translation_of")
    ];

    private static IReadOnlyList<KnowledgeImportManifestation>
        CreateManifestations() =>
    [
        new(
            VolumeManifestationId,
            "approved",
            VolumeExpressionId,
            "Nicene and Post-Nicene Fathers, Second Series, Volume IV",
            null,
            "Edinburgh; Grand Rapids, Michigan",
            "NPNF2-04: Athanasius — Select Works and Letters"),
        new(
            ManifestationId,
            "approved",
            EnglishExpressionId,
            "De Decretis as contained in NPNF Second Series, Volume IV",
            null,
            "Edinburgh; Grand Rapids, Michigan",
            "NPNF2-04, De Decretis")
    ];

    private static IReadOnlyList<
        KnowledgeImportManifestationIdentifier>
        CreateManifestationIdentifiers() =>
    [
        new(
            VolumeManifestationId,
            "ccel",
            "npnf204",
            DeDecretisDocument.SourceUri)
    ];

    private static IReadOnlyList<KnowledgeImportContributor>
        CreateContributors() =>
    [
        new(
            AthanasiusId,
            "approved",
            "person",
            "Athanasius of Alexandria",
            "Athanasius of Alexandria",
            "Author of De Decretis in the curated NPNF2-04 source profile."),
        new(
            NewmanId,
            "approved",
            "person",
            "John Henry Newman",
            "Newman, John Henry",
            "Translator whose earlier English work is identified by the NPNF2-04 preface as the basis for this material."),
        new(
            RobertsonId,
            "approved",
            "person",
            "Archibald Robertson",
            "Robertson, Archibald",
            "Editor of NPNF2-04; the volume preface describes revision of earlier translations and notes.")
    ];

    private static IReadOnlyList<KnowledgeImportContribution>
        CreateContributions() =>
    [
        new(
            AthanasiusId,
            WorkId,
            null,
            null,
            "author",
            "established",
            0),
        new(
            NewmanId,
            null,
            EnglishExpressionId,
            null,
            "translator",
            "established",
            0),
        new(
            RobertsonId,
            null,
            EnglishExpressionId,
            null,
            "reviser",
            "explicit",
            1),
        new(
            RobertsonId,
            null,
            VolumeExpressionId,
            null,
            "textual_editor",
            "explicit",
            0)
    ];

    private static IReadOnlyList<KnowledgeImportArtifact>
        CreateArtifacts(
            PreparedDeDecretis prepared) =>
    [
        new(
            RawArtifactId,
            "approved",
            VolumeManifestationId,
            null,
            "raw",
            prepared.RawSha256,
            "application/pdf",
            prepared.RawByteLength,
            DeDecretisDocument.SourceUri,
            "active",
            ".pdf",
            prepared.SourcePath,
            null),
        new(
            ParsedArtifactId,
            "approved",
            ManifestationId,
            RawArtifactId,
            "parsed",
            prepared.ParsedArtifact.Sha256,
            "text/plain; charset=utf-8",
            prepared.ParsedArtifact.Bytes.LongLength,
            null,
            "active",
            ".txt",
            null,
            prepared.ParsedArtifact.Bytes),
        new(
            NormalizedArtifactId,
            "approved",
            ManifestationId,
            ParsedArtifactId,
            "normalized",
            prepared.NormalizedArtifact.Sha256,
            "text/plain; charset=utf-8",
            prepared.NormalizedArtifact.Bytes.LongLength,
            null,
            "active",
            ".txt",
            null,
            prepared.NormalizedArtifact.Bytes)
    ];

    private static IReadOnlyList<
        KnowledgeImportProcessingActivity>
        CreateProcessingActivities() =>
    [
        new(
            RawArtifactId,
            ParsedArtifactId,
            "parse",
            "PdfPig",
            "0.1.15",
            BuildParserConfigurationJson(),
            "ApologiaStudio.KnowledgeImporter",
            "completed"),
        new(
            ParsedArtifactId,
            NormalizedArtifactId,
            "normalize",
            "ApologiaStudio.KnowledgeImporter",
            DeDecretisDocument.ProfileId,
            "{\"chapterHeadings\":\"excluded\",\"lineBreakHyphens\":\"preserved\",\"unicodeNormalization\":\"NFC\"}",
            "ApologiaStudio.KnowledgeImporter",
            "completed")
    ];

    private static IReadOnlyList<KnowledgeImportSegment>
        CreateSegments(
            PreparedDeDecretis prepared) =>
        prepared.Segments
            .OrderBy(segment => segment.Number)
            .Select(segment => new KnowledgeImportSegment(
                segment.Id,
                "approved",
                NormalizedArtifactId,
                null,
                DocumentSegmentType.Section,
                DocumentSegmentKind.MainText,
                segment.Number,
                $"Section {segment.Number}",
                segment.Text,
                segment.Locator))
            .ToArray();

    private static IReadOnlyList<
        KnowledgeImportClassificationTerm>
        CreateClassificationTerms() =>
    [
        new(
            KnowledgeClassificationDimension.SourceKind,
            "primary_source",
            "Primary source",
            "A source produced by a historical participant or witness relevant to the question under study.",
            null),
        new(
            KnowledgeClassificationDimension.Perspective,
            "pro_nicene",
            "Pro-Nicene",
            "Analytical classification for fourth-century material defending the Nicene settlement.",
            "Fourth century"),
        new(
            KnowledgeClassificationDimension.EvidenceRole,
            "historical_witness",
            "Historical witness",
            "Evidence for what a historical actor reports, argues, or remembers.",
            null),
        new(
            KnowledgeClassificationDimension.EvidenceRole,
            "theological_argument",
            "Theological argument",
            "Evidence used to analyze a theological argument in its historical source.",
            null)
    ];

    private static IReadOnlyList<
        KnowledgeImportClassificationAssertion>
        CreateClassificationAssertions() =>
    [
        new(
            StableKnowledgeIds.ForProfile(
                "assertion:source-kind:primary-source"),
            WorkId,
            KnowledgeClassificationDimension.SourceKind,
            "primary_source",
            null,
            "editorial",
            EditorialActor,
            "verified",
            EditorialActor,
            "De Decretis is authored by Athanasius and is used here as a primary source for his fourth-century Nicene argument.",
            null,
            null),
        new(
            StableKnowledgeIds.ForProfile(
                "assertion:perspective:pro-nicene"),
            WorkId,
            KnowledgeClassificationDimension.Perspective,
            "pro_nicene",
            "analytical",
            "editorial",
            EditorialActor,
            "verified",
            EditorialActor,
            "Editorial classification based on the work's explicit defence of the Nicene definition.",
            null,
            null),
        new(
            StableKnowledgeIds.ForProfile(
                "assertion:evidence-role:historical-witness"),
            WorkId,
            KnowledgeClassificationDimension.EvidenceRole,
            "historical_witness",
            null,
            "editorial",
            EditorialActor,
            "verified",
            EditorialActor,
            "The work contains Athanasius's account of the Nicene controversy and proceedings.",
            null,
            null),
        new(
            StableKnowledgeIds.ForProfile(
                "assertion:evidence-role:theological-argument"),
            WorkId,
            KnowledgeClassificationDimension.EvidenceRole,
            "theological_argument",
            null,
            "editorial",
            EditorialActor,
            "verified",
            EditorialActor,
            "The work explicitly argues for the wording and meaning of the Nicene definition.",
            null,
            null)
    ];

    private static IReadOnlyList<KnowledgeImportMetadataAssertion>
        CreateMetadataAssertions() =>
    [
        new(
            StableKnowledgeIds.ForProfile(
                "assertion:raw:pdf-page-count"),
            RawArtifactId,
            "pdf_page_count",
            DeDecretisDocument.ExpectedPdfPageCount.ToString(
                CultureInfo.InvariantCulture),
            "imported",
            EditorialActor,
            "verified",
            EditorialActor,
            null,
            "Verified from the acquired PDF artifact during ingestion.",
            null,
            null),
        new(
            StableKnowledgeIds.ForProfile(
                "assertion:normalized:source-page-range"),
            NormalizedArtifactId,
            "source_pdf_page_range",
            $"{DeDecretisDocument.FirstPdfPage}-{DeDecretisDocument.LastPdfPage}",
            "imported",
            EditorialActor,
            "verified",
            EditorialActor,
            null,
            "The selected PDF pages correspond to printed NPNF pages 482–531 and end before De Sententia Dionysii.",
            null,
            null)
    ];

    private static string BuildParserConfigurationJson() =>
        $$"""
        {
          "profile": "{{DeDecretisDocument.ProfileId}}",
          "pdfPageStart": {{DeDecretisDocument.FirstPdfPage}},
          "pdfPageEnd": {{DeDecretisDocument.LastPdfPage}},
          "minimumFontSize": {{DeDecretisDocument.MinimumFontSize.ToString(CultureInfo.InvariantCulture)}},
          "minimumBaselineY": {{DeDecretisDocument.MinimumBaselineY.ToString(CultureInfo.InvariantCulture)}},
          "maximumBaselineY": {{DeDecretisDocument.MaximumBaselineY.ToString(CultureInfo.InvariantCulture)}},
          "excluded": ["running headers", "page numbers", "editorial footnotes"]
        }
        """;
}
