namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record BibleCorpusImportRequest
{
    public BibleCorpusImportRequest(
        BibleEditionImportDefinition edition,
        BibleCorpusReadRequest corpusReadRequest,
        BibleCorpusValidationEvidence validationEvidence,
        IEnumerable<BibleSourceArtifactImport> sourceArtifacts,
        string? upstreamRevision = null)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(corpusReadRequest);
        ArgumentNullException.ThrowIfNull(validationEvidence);
        ArgumentNullException.ThrowIfNull(sourceArtifacts);

        var artifacts = sourceArtifacts.ToArray();
        if (artifacts.Length == 0)
        {
            throw new ArgumentException("At least one source artifact is required.", nameof(sourceArtifacts));
        }

        if (artifacts.Count(artifact => artifact.Role == BibleSourceArtifactRole.CanonicalUsfm) != 1)
        {
            throw new ArgumentException(
                "Exactly one canonical USFM artifact is required.",
                nameof(sourceArtifacts));
        }

        if (artifacts
            .GroupBy(artifact => (artifact.Role, artifact.FileName))
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Artifact role and file-name pairs must be unique.",
                nameof(sourceArtifacts));
        }

        if (upstreamRevision is not null && upstreamRevision.Trim().Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(upstreamRevision));
        }

        Edition = edition;
        CorpusReadRequest = corpusReadRequest;
        ValidationEvidence = validationEvidence;
        SourceArtifacts = artifacts;
        UpstreamRevision = string.IsNullOrWhiteSpace(upstreamRevision)
            ? null
            : upstreamRevision.Trim();
    }

    public BibleEditionImportDefinition Edition { get; }

    public BibleCorpusReadRequest CorpusReadRequest { get; }

    public BibleCorpusValidationEvidence ValidationEvidence { get; }

    public IReadOnlyList<BibleSourceArtifactImport> SourceArtifacts { get; }

    public string? UpstreamRevision { get; }
}
