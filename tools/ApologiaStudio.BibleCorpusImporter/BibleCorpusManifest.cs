using System.Text.Json.Serialization;

namespace ApologiaStudio.BibleCorpusImporter;

public sealed class BibleCorpusManifest
{
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = string.Empty;

    public int SchemaVersion { get; init; }

    public string ManifestId { get; init; } = string.Empty;

    public ManifestEdition Edition { get; init; } = new();

    public ManifestSource Source { get; init; } = new();

    public ManifestSelection Selection { get; init; } = new();

    public ManifestProcessing Processing { get; init; } = new();

    public ManifestValidation Validation { get; init; } = new();

    public ManifestEditorialAudit EditorialAudit { get; init; } = new();
}

public sealed class ManifestEdition
{
    public string Code { get; init; } = string.Empty;

    public string ApprovedCorpusCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string LanguageTag { get; init; } = string.Empty;

    public string CanonCode { get; init; } = string.Empty;

    public ManifestLicense License { get; init; } = new();
}

public sealed class ManifestLicense
{
    public string Status { get; init; } = string.Empty;

    public string SourceUri { get; init; } = string.Empty;

    public string? Notice { get; init; }
}

public sealed class ManifestSource
{
    public string Provider { get; init; } = string.Empty;

    public string UpstreamDistributionId { get; init; } = string.Empty;

    public string DetailsUri { get; init; } = string.Empty;

    public DateTimeOffset CapturedAt { get; init; }

    public IReadOnlyList<ManifestArtifact> Artifacts { get; init; } = [];
}

public sealed class ManifestArtifact
{
    public string Role { get; init; } = string.Empty;

    public string Uri { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long ByteLength { get; init; }
}

public sealed class ManifestSelection
{
    public string Policy { get; init; } = string.Empty;

    public int UsfmIncludedBookCount { get; init; }

    public int VplIncludedBookCount { get; init; }

    public IReadOnlyList<string> ExcludedUsfmIds { get; init; } = [];
}

public sealed class ManifestProcessing
{
    public string CanonicalFormat { get; init; } = string.Empty;

    public string ValidationFormat { get; init; } = string.Empty;

    public string StoredFormat { get; init; } = string.Empty;

    public ManifestParser Parser { get; init; } = new();

    public string NormalizationPolicyId { get; init; } = string.Empty;
}

public sealed class ManifestParser
{
    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;
}

public sealed class ManifestValidation
{
    public string Status { get; init; } = string.Empty;

    public DateOnly ValidatedOn { get; init; }

    public string ValidatorCommit { get; init; } = string.Empty;

    public bool SourceIntegrityValidated { get; init; }

    public bool FormatParityValidated { get; init; }

    public bool ReferenceParityValidated { get; init; }

    public bool TextParityValidated { get; init; }

    public int UsfmFileCount { get; init; }

    public int UsfmBookCount { get; init; }

    public int UsfmVerseCount { get; init; }

    public int VplFileCount { get; init; }

    public int VplBookCount { get; init; }

    public int VplVerseCount { get; init; }

    public long StrongAttributeCount { get; init; }

    public int MissingFromUsfm { get; init; }

    public int UnexpectedInUsfm { get; init; }

    public int TextMismatches { get; init; }

    public string ReportPath { get; init; } = string.Empty;
}

public sealed class ManifestEditorialAudit
{
    public string Status { get; init; } = string.Empty;

    public bool BlocksImport { get; init; }
}
