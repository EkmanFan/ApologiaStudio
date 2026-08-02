using System.IO.Compression;
using System.Security.Cryptography;
using ApologiaStudio.BibleCorpusImporter;

namespace ApologiaStudio.UnitTests.Tools.BibleCorpusImporter;

public sealed class BibleCorpusManifestLoaderTests
{
    [Fact]
    public async Task Load_ShouldAcceptStrictApprovedManifest()
    {
        using var fixture = new ManifestFixture();
        var manifestPath = await fixture.WriteManifestAsync();

        var manifest = await BibleCorpusManifestLoader.LoadAsync(
            manifestPath,
            CancellationToken.None);

        Assert.Equal("fixture-2026-08-02", manifest.ManifestId);
        Assert.Equal("fixture", manifest.Edition.Code);
        Assert.Equal(66, manifest.Validation.UsfmBookCount);
        Assert.Equal(2, manifest.Source.Artifacts.Count);
    }

    [Fact]
    public async Task Load_ShouldRejectUnknownManifestProperties()
    {
        using var fixture = new ManifestFixture();
        var json = fixture.CreateManifestJson()
            .Replace(
                "\"schemaVersion\": 1,",
                "\"schemaVersion\": 1, \"unexpected\": true,",
                StringComparison.Ordinal);
        var manifestPath = await fixture.WriteManifestAsync(json);

        var exception = await Assert.ThrowsAsync<BibleCorpusManifestException>(() =>
            BibleCorpusManifestLoader.LoadAsync(manifestPath, CancellationToken.None));

        Assert.Contains("unexpected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Load_ShouldRejectProcessingPolicyDrift()
    {
        using var fixture = new ManifestFixture();
        var json = fixture.CreateManifestJson()
            .Replace("\"canonicalFormat\": \"USFM\"", "\"canonicalFormat\": \"XML\"", StringComparison.Ordinal);
        var manifestPath = await fixture.WriteManifestAsync(json);

        var exception = await Assert.ThrowsAsync<BibleCorpusManifestException>(() =>
            BibleCorpusManifestLoader.LoadAsync(manifestPath, CancellationToken.None));

        Assert.Contains("USFM must be the canonical format", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preparation_ShouldRejectArchivePathTraversal()
    {
        using var fixture = new ManifestFixture();
        var escapeFileName = $"apologia-escape-{Guid.NewGuid():N}.usfm";
        var escapePath = Path.Combine(Path.GetTempPath(), escapeFileName);
        fixture.CreateCanonicalArchive($"../../{escapeFileName}");
        fixture.CreateValidationArchive();
        var manifestPath = await fixture.WriteManifestAsync();
        var manifest = await BibleCorpusManifestLoader.LoadAsync(
            manifestPath,
            CancellationToken.None);

        try
        {
            var exception = await Assert.ThrowsAsync<BibleCorpusManifestException>(() =>
                ManifestImportPreparation.CreateAsync(
                    manifest,
                    fixture.ArtifactsDirectory,
                    CancellationToken.None));

            Assert.Contains("escapes the extraction directory", exception.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(escapePath));
        }
        finally
        {
            if (File.Exists(escapePath))
            {
                File.Delete(escapePath);
            }
        }
    }

    private sealed class ManifestFixture : IDisposable
    {
        private const string CanonicalFileName = "fixture_usfm.zip";
        private const string ValidationFileName = "fixture_vpl.zip";

        public ManifestFixture()
        {
            RootDirectory = Path.Combine(
                Path.GetTempPath(),
                $"apologia-manifest-tests-{Guid.NewGuid():N}");
            ArtifactsDirectory = Path.Combine(RootDirectory, "artifacts");
            Directory.CreateDirectory(ArtifactsDirectory);
        }

        public string RootDirectory { get; }

        public string ArtifactsDirectory { get; }

        public void CreateCanonicalArchive(string entryName = "GEN.usfm") =>
            CreateArchive(CanonicalFileName, entryName, "\\id GEN\n\\c 1\n\\v 1 In the beginning.");

        public void CreateValidationArchive() =>
            CreateArchive(ValidationFileName, "fixture.txt", "GEN 1:1 In the beginning.");

        public async Task<string> WriteManifestAsync(string? json = null)
        {
            var path = Path.Combine(RootDirectory, "fixture-2026-08-02.json");
            await File.WriteAllTextAsync(path, json ?? CreateManifestJson());
            return path;
        }

        public string CreateManifestJson()
        {
            EnsureArchives();
            var canonical = GetArtifact(CanonicalFileName);
            var validation = GetArtifact(ValidationFileName);

            return $$"""
                {
                  "$schema": "./bible-corpus-manifest.schema.json",
                  "schemaVersion": 1,
                  "manifestId": "fixture-2026-08-02",
                  "edition": {
                    "code": "fixture",
                    "approvedCorpusCode": "fixture",
                    "displayName": "Fixture Bible",
                    "languageTag": "en",
                    "canonCode": "protestant-66",
                    "license": {
                      "status": "public-domain",
                      "sourceUri": "https://example.test/fixture"
                    }
                  },
                  "source": {
                    "provider": "eBible.org",
                    "upstreamDistributionId": "fixture",
                    "detailsUri": "https://example.test/fixture",
                    "capturedAt": "2026-08-02T18:00:00Z",
                    "artifacts": [
                      {
                        "role": "canonical-usfm",
                        "uri": "https://example.test/fixture_usfm.zip",
                        "fileName": "{{CanonicalFileName}}",
                        "sha256": "{{canonical.Digest}}",
                        "byteLength": {{canonical.Length}}
                      },
                      {
                        "role": "validation-vpl",
                        "uri": "https://example.test/fixture_vpl.zip",
                        "fileName": "{{ValidationFileName}}",
                        "sha256": "{{validation.Digest}}",
                        "byteLength": {{validation.Length}}
                      }
                    ]
                  },
                  "selection": {
                    "policy": "protestant-66-only",
                    "usfmIncludedBookCount": 66,
                    "vplIncludedBookCount": 66,
                    "excludedUsfmIds": []
                  },
                  "processing": {
                    "canonicalFormat": "USFM",
                    "validationFormat": "VPL",
                    "storedFormat": "normalized-relational",
                    "parser": { "name": "SIL.Machine", "version": "3.9.1" },
                    "normalizationPolicyId": "unicode-nfc-collapse-whitespace-v1"
                  },
                  "validation": {
                    "status": "passed",
                    "validatedOn": "2026-08-02",
                    "validatorCommit": "40fb1d6",
                    "sourceIntegrityValidated": true,
                    "formatParityValidated": true,
                    "referenceParityValidated": true,
                    "textParityValidated": true,
                    "usfmFileCount": 66,
                    "usfmBookCount": 66,
                    "usfmVerseCount": 66,
                    "vplFileCount": 1,
                    "vplBookCount": 66,
                    "vplVerseCount": 66,
                    "strongAttributeCount": 0,
                    "missingFromUsfm": 0,
                    "unexpectedInUsfm": 0,
                    "textMismatches": 0,
                    "reportPath": "artifacts/bible-corpus-validation/fixture.json"
                  },
                  "editorialAudit": { "status": "deferred", "blocksImport": false }
                }
                """;
        }

        public void Dispose() => Directory.Delete(RootDirectory, recursive: true);

        private void EnsureArchives()
        {
            if (!File.Exists(Path.Combine(ArtifactsDirectory, CanonicalFileName)))
            {
                CreateCanonicalArchive();
            }

            if (!File.Exists(Path.Combine(ArtifactsDirectory, ValidationFileName)))
            {
                CreateValidationArchive();
            }
        }

        private void CreateArchive(string fileName, string entryName, string content)
        {
            var path = Path.Combine(ArtifactsDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            var entry = archive.CreateEntry(entryName, CompressionLevel.SmallestSize);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        private (string Digest, long Length) GetArtifact(string fileName)
        {
            var bytes = File.ReadAllBytes(Path.Combine(ArtifactsDirectory, fileName));
            return (
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                bytes.LongLength);
        }
    }
}
