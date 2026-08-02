using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

internal sealed class BibleCorpusVersionEntity
{
    public BibleCorpusVersionId Id { get; set; }

    public BibleEditionCode EditionCode { get; set; }

    public string? UpstreamRevision { get; set; }

    public Sha256Digest SourceTreeSha256 { get; set; }

    public Sha256Digest ImportFingerprint { get; set; }

    public string ParserName { get; set; } = string.Empty;

    public string ParserVersion { get; set; } = string.Empty;

    public string NormalizationPolicyId { get; set; } = string.Empty;

    public int CanonicalSchemaVersion { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string ValidationStatus { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
