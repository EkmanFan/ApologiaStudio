using System.IO.Compression;
using System.Security.Cryptography;
using ApologiaStudio.BibleCorpusImporter;
using ApologiaStudio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class BibleCorpusImportCliTests
{
    private static readonly string[] ProtestantBookCodes =
    [
        "GEN", "EXO", "LEV", "NUM", "DEU", "JOS", "JDG", "RUT", "1SA", "2SA",
        "1KI", "2KI", "1CH", "2CH", "EZR", "NEH", "EST", "JOB", "PSA", "PRO",
        "ECC", "SNG", "ISA", "JER", "LAM", "EZK", "DAN", "HOS", "JOL", "AMO",
        "OBA", "JON", "MIC", "NAM", "HAB", "ZEP", "HAG", "ZEC", "MAL", "MAT",
        "MRK", "LUK", "JHN", "ACT", "ROM", "1CO", "2CO", "GAL", "EPH", "PHP",
        "COL", "1TH", "2TH", "1TI", "2TI", "TIT", "PHM", "HEB", "JAS", "1PE",
        "2PE", "1JN", "2JN", "3JN", "JUD", "REV"
    ];

    [Fact]
    public async Task Run_ShouldImportVerifiedManifestAndRemainIdempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_TEST_DB_CONNECTION");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        await using var context = CreateContext(connectionString!);
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        using var fixture = new CorpusImportFixture();
        var previousConnection = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_DB_CONNECTION");
        Environment.SetEnvironmentVariable(
            "APOLOGIASTUDIO_DB_CONNECTION",
            connectionString);

        try
        {
            var args = new[]
            {
                "--manifest", fixture.ManifestPath,
                "--artifacts", fixture.ArtifactsDirectory,
                "--confirm-manifest", "fixture-2026-08-02"
            };

            var firstExitCode = await ImportCli.RunAsync(args, CancellationToken.None);
            var secondExitCode = await ImportCli.RunAsync(args, CancellationToken.None);

            Assert.Equal(0, firstExitCode);
            Assert.Equal(0, secondExitCode);

            await context.Database.OpenConnectionAsync();
            Assert.Equal(1L, await ScalarInt64Async(context, "SELECT COUNT(*) FROM bible_editions"));
            Assert.Equal(1L, await ScalarInt64Async(context, "SELECT COUNT(*) FROM bible_corpus_versions"));
            Assert.Equal(1L, await ScalarInt64Async(context, "SELECT COUNT(*) FROM bible_corpus_versions WHERE is_active"));
            Assert.Equal(66L, await ScalarInt64Async(context, "SELECT COUNT(*) FROM bible_corpus_books"));
            Assert.Equal(66L, await ScalarInt64Async(context, "SELECT COUNT(*) FROM bible_verses"));
            Assert.Equal(2L, await ScalarInt64Async(context, "SELECT COUNT(*) FROM bible_source_artifacts"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "APOLOGIASTUDIO_DB_CONNECTION",
                previousConnection);
        }
    }

    private static ApologiaStudioDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApologiaStudioDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ApologiaStudioDbContext(options);
    }

    private static async Task<long> ScalarInt64Async(
        ApologiaStudioDbContext context,
        string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private sealed class CorpusImportFixture : IDisposable
    {
        private const string CanonicalFileName = "fixture_usfm.zip";
        private const string ValidationFileName = "fixture_vpl.zip";

        public CorpusImportFixture()
        {
            RootDirectory = Path.Combine(
                Path.GetTempPath(),
                $"apologia-import-cli-tests-{Guid.NewGuid():N}");
            ArtifactsDirectory = Path.Combine(RootDirectory, "artifacts");
            Directory.CreateDirectory(ArtifactsDirectory);

            CreateCanonicalArchive();
            CreateValidationArchive();
            ManifestPath = Path.Combine(RootDirectory, "fixture-2026-08-02.json");
            File.WriteAllText(ManifestPath, CreateManifestJson());
        }

        public string RootDirectory { get; }

        public string ArtifactsDirectory { get; }

        public string ManifestPath { get; }

        public void Dispose() => Directory.Delete(RootDirectory, recursive: true);

        private void CreateCanonicalArchive()
        {
            using var archive = ZipFile.Open(
                Path.Combine(ArtifactsDirectory, CanonicalFileName),
                ZipArchiveMode.Create);

            for (var index = 0; index < ProtestantBookCodes.Length; index++)
            {
                var code = ProtestantBookCodes[index];
                var entry = archive.CreateEntry(
                    $"fixture/{index + 1:D2}-{code}.usfm",
                    CompressionLevel.SmallestSize);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(
                    $"\\id {code} Fixture\n\\h {code}\n\\toc2 {code}\n\\c 1\n\\p\n\\v 1 Fixture text for {code}.");
            }
        }

        private void CreateValidationArchive()
        {
            using var archive = ZipFile.Open(
                Path.Combine(ArtifactsDirectory, ValidationFileName),
                ZipArchiveMode.Create);
            var entry = archive.CreateEntry("fixture_vpl.txt", CompressionLevel.SmallestSize);
            using var writer = new StreamWriter(entry.Open());
            foreach (var code in ProtestantBookCodes)
            {
                writer.WriteLine($"{code} 1:1 Fixture text for {code}.");
            }
        }

        private string CreateManifestJson()
        {
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

        private (string Digest, long Length) GetArtifact(string fileName)
        {
            var bytes = File.ReadAllBytes(Path.Combine(ArtifactsDirectory, fileName));
            return (
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                bytes.LongLength);
        }
    }
}
