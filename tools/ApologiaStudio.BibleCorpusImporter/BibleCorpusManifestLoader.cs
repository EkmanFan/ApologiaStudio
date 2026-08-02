using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.BibleCorpusImporter;

public static partial class BibleCorpusManifestLoader
{
    private const string ExpectedSchema = "./bible-corpus-manifest.schema.json";
    private const string CanonCode = "protestant-66";
    private const string CanonicalFormat = "USFM";
    private const string ValidationFormat = "VPL";
    private const string StoredFormat = "normalized-relational";
    private const string ParserName = "SIL.Machine";
    private const string ParserVersion = "3.9.1";
    private const string NormalizationPolicy = "unicode-nfc-collapse-whitespace-v1";
    private const int CanonicalBookCount = 66;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<BibleCorpusManifest> LoadAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var fullPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullPath))
        {
            throw new BibleCorpusManifestException($"Manifest does not exist: {fullPath}");
        }

        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var manifest = await JsonSerializer.DeserializeAsync<BibleCorpusManifest>(
                stream,
                JsonOptions,
                cancellationToken);
            if (manifest is null)
            {
                throw new BibleCorpusManifestException("Manifest JSON contains no object.");
            }

            Validate(manifest, fullPath);
            return manifest;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BibleCorpusManifestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or ArgumentException)
        {
            throw new BibleCorpusManifestException(
                $"Unable to load manifest {fullPath}: {exception.Message}",
                exception);
        }
    }

    private static void Validate(BibleCorpusManifest manifest, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(manifest.Edition);
        ArgumentNullException.ThrowIfNull(manifest.Edition.License);
        ArgumentNullException.ThrowIfNull(manifest.Source);
        ArgumentNullException.ThrowIfNull(manifest.Source.Artifacts);
        ArgumentNullException.ThrowIfNull(manifest.Selection);
        ArgumentNullException.ThrowIfNull(manifest.Selection.ExcludedUsfmIds);
        ArgumentNullException.ThrowIfNull(manifest.Processing);
        ArgumentNullException.ThrowIfNull(manifest.Processing.Parser);
        ArgumentNullException.ThrowIfNull(manifest.Validation);
        ArgumentNullException.ThrowIfNull(manifest.EditorialAudit);

        Require(manifest.Schema == ExpectedSchema, "Unsupported manifest $schema.");
        Require(manifest.SchemaVersion == 1, "Unsupported manifest schemaVersion.");
        Require(ManifestIdRegex().IsMatch(manifest.ManifestId), "manifestId is invalid.");
        Require(
            string.Equals(
                Path.GetFileNameWithoutExtension(fullPath),
                manifest.ManifestId,
                StringComparison.Ordinal),
            "manifestId must match the manifest file name.");

        _ = new BibleEditionCode(manifest.Edition.Code);
        RequireNotBlank(manifest.Edition.ApprovedCorpusCode, "edition.approvedCorpusCode");
        Require(
            manifest.ManifestId.StartsWith(
                $"{manifest.Edition.ApprovedCorpusCode}-",
                StringComparison.Ordinal),
            "manifestId must begin with edition.approvedCorpusCode.");
        RequireNotBlank(manifest.Edition.DisplayName, "edition.displayName");
        RequireNotBlank(manifest.Edition.LanguageTag, "edition.languageTag");
        Require(manifest.Edition.CanonCode == CanonCode, "Only the Protestant 66-book canon is supported.");
        Require(manifest.Edition.License.Status == "public-domain", "Only approved public-domain editions are supported.");
        _ = RequireHttpsUri(manifest.Edition.License.SourceUri, "edition.license.sourceUri");

        Require(manifest.Source.Provider == "eBible.org", "Unsupported source provider.");
        RequireNotBlank(manifest.Source.UpstreamDistributionId, "source.upstreamDistributionId");
        _ = RequireHttpsUri(manifest.Source.DetailsUri, "source.detailsUri");
        Require(manifest.Source.CapturedAt != default, "source.capturedAt is required.");
        Require(manifest.Source.Artifacts.Count == 2, "Exactly two source artifacts are required.");

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in manifest.Source.Artifacts)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            Require(
                artifact.Role is "canonical-usfm" or "validation-vpl",
                $"Unsupported artifact role: {artifact.Role}");
            Require(roles.Add(artifact.Role), $"Duplicate artifact role: {artifact.Role}");
            RequireSafeFileName(artifact.FileName);
            Require(
                string.Equals(Path.GetExtension(artifact.FileName), ".zip", StringComparison.OrdinalIgnoreCase),
                $"Artifact must be a ZIP archive: {artifact.FileName}");
            _ = RequireHttpsUri(artifact.Uri, $"artifact {artifact.FileName} URI");
            _ = new Sha256Digest(artifact.Sha256);
            Require(artifact.ByteLength > 0, $"Artifact {artifact.FileName} byteLength must be positive.");
        }

        Require(roles.SetEquals(["canonical-usfm", "validation-vpl"]), "Canonical USFM and validation VPL artifacts are required.");
        Require(manifest.Selection.Policy == "protestant-66-only", "Unsupported selection policy.");
        Require(
            manifest.Selection.UsfmIncludedBookCount == CanonicalBookCount
            && manifest.Selection.VplIncludedBookCount == CanonicalBookCount,
            "The manifest must select exactly 66 USFM and VPL books.");
        Require(
            manifest.Selection.ExcludedUsfmIds.Distinct(StringComparer.Ordinal).Count()
            == manifest.Selection.ExcludedUsfmIds.Count,
            "selection.excludedUsfmIds contains duplicates.");
        foreach (var code in manifest.Selection.ExcludedUsfmIds)
        {
            _ = new UsfmBookCode(code);
        }

        Require(manifest.Processing.CanonicalFormat == CanonicalFormat, "USFM must be the canonical format.");
        Require(manifest.Processing.ValidationFormat == ValidationFormat, "VPL must be the validation format.");
        Require(manifest.Processing.StoredFormat == StoredFormat, "Unsupported stored format.");
        Require(
            manifest.Processing.Parser.Name == ParserName
            && manifest.Processing.Parser.Version == ParserVersion,
            "Manifest parser does not match the production parser.");
        Require(
            manifest.Processing.NormalizationPolicyId == NormalizationPolicy,
            "Manifest normalization policy does not match the production importer.");

        var validation = manifest.Validation;
        Require(validation.Status == "passed", "Manifest validation status is not passed.");
        Require(validation.ValidatedOn != default, "validation.validatedOn is required.");
        Require(CommitRegex().IsMatch(validation.ValidatorCommit), "validation.validatorCommit is invalid.");
        Require(
            validation.SourceIntegrityValidated
            && validation.FormatParityValidated
            && validation.ReferenceParityValidated
            && validation.TextParityValidated,
            "All source-integrity and USFM/VPL parity checks must have passed.");
        Require(
            validation.UsfmFileCount > 0
            && validation.VplFileCount > 0
            && validation.UsfmBookCount == CanonicalBookCount
            && validation.VplBookCount == CanonicalBookCount,
            "Validation evidence must cover both 66-book corpora.");
        Require(
            validation.UsfmVerseCount > 0
            && validation.UsfmVerseCount == validation.VplVerseCount,
            "USFM and VPL validation verse counts must match.");
        Require(validation.StrongAttributeCount >= 0, "Strong attribute count cannot be negative.");
        Require(
            validation.MissingFromUsfm == 0
            && validation.UnexpectedInUsfm == 0
            && validation.TextMismatches == 0,
            "Manifest records unresolved USFM/VPL differences.");
        RequireNotBlank(validation.ReportPath, "validation.reportPath");
        Require(
            manifest.EditorialAudit.Status == "deferred" && !manifest.EditorialAudit.BlocksImport,
            "Editorial audit policy blocks this import.");
    }

    private static Uri RequireHttpsUri(string value, string field)
    {
        Require(
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps,
            $"{field} must be an absolute HTTPS URI.");
        return uri!;
    }

    private static void RequireSafeFileName(string value)
    {
        RequireNotBlank(value, "artifact.fileName");
        Require(
            value == Path.GetFileName(value)
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0,
            $"Artifact fileName is unsafe: {value}");
    }

    private static void RequireNotBlank(string value, string field) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{field} is required.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new BibleCorpusManifestException(message);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ManifestIdRegex();

    [GeneratedRegex("^[0-9a-f]{7,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitRegex();
}
