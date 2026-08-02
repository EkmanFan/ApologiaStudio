using System.Data;
using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.BibleCorpora;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;

public sealed class PostgreSqlBibleCorpusImporter(
    ApologiaStudioDbContext dbContext,
    IBibleCorpusReader corpusReader,
    TimeProvider timeProvider)
    : IBibleCorpusImporter
{
    private const string CanonCode = "protestant-66";
    private const int CanonicalBookCount = 66;
    private const string ParserName = "SIL.Machine";
    private const string ParserVersion = "3.9.1";
    private const string NormalizationPolicyId = "unicode-nfc-collapse-whitespace-v1";
    private const int CanonicalSchemaVersion = 1;
    private static readonly TimeSpan BulkImportTimeout = TimeSpan.FromMinutes(10);

    public async Task<BibleCorpusImportResult> ImportAsync(
        BibleCorpusImportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateImportPolicy(request);

        await BibleCorpusImportHashing.VerifyArtifactsAsync(
            request.SourceArtifacts,
            cancellationToken);

        var parsedCorpus = await corpusReader.ReadAsync(
            request.CorpusReadRequest,
            cancellationToken);

        var (annotationCount, strongAttributeCount) = ValidateParsedCorpus(request, parsedCorpus);
        var sourceTreeDigest = BibleCorpusImportHashing.ComputeSourceTreeDigest(
            parsedCorpus.Books);
        var importFingerprint = BibleCorpusImportHashing.ComputeImportFingerprint(
            request.Edition.Code,
            sourceTreeDigest,
            ParserName,
            ParserVersion,
            NormalizationPolicyId,
            CanonicalSchemaVersion);

        var importedAt = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await AcquireEditionLockAsync(request.Edition.Code, cancellationToken);
        await EnsureEditionAsync(request.Edition, cancellationToken);

        var existingVersion = await dbContext.Set<BibleCorpusVersionEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                version => version.ImportFingerprint == importFingerprint,
                cancellationToken);

        if (existingVersion is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return CreateResult(
                existingVersion.Id,
                importFingerprint,
                wasCreated: false,
                parsedCorpus,
                annotationCount,
                strongAttributeCount);
        }

        var versionId = BibleCorpusVersionId.New();
        dbContext.Set<BibleCorpusVersionEntity>().Add(
            new BibleCorpusVersionEntity
            {
                Id = versionId,
                EditionCode = request.Edition.Code,
                UpstreamRevision = request.UpstreamRevision,
                SourceTreeSha256 = sourceTreeDigest,
                ImportFingerprint = importFingerprint,
                ParserName = ParserName,
                ParserVersion = ParserVersion,
                NormalizationPolicyId = NormalizationPolicyId,
                CanonicalSchemaVersion = CanonicalSchemaVersion,
                ImportedAt = importedAt,
                ApprovedAt = null,
                ValidationStatus = "pending",
                IsActive = false
            });

        dbContext.Set<BibleCorpusBookEntity>().AddRange(
            parsedCorpus.Books.Select(book =>
                new BibleCorpusBookEntity
                {
                    CorpusVersionId = versionId,
                    UsfmBookCode = book.BookCode,
                    BookOrdinal = book.BookOrdinal,
                    DisplayName = book.DisplayName,
                    ShortName = book.ShortName,
                    SourceRelativePath = book.SourceRelativePath
                }));

        dbContext.Set<BibleSourceArtifactEntity>().AddRange(
            request.SourceArtifacts.Select(artifact =>
                new BibleSourceArtifactEntity
                {
                    CorpusVersionId = versionId,
                    Role = ToDatabaseRole(artifact.Role),
                    SourceUri = artifact.SourceUri.AbsoluteUri,
                    FileName = artifact.FileName,
                    Sha256 = artifact.ExpectedSha256,
                    ByteLength = artifact.ExpectedByteLength,
                    DownloadedAt = artifact.DownloadedAt
                }));

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var npgsqlTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        var verseIds = await AllocateVerseIdsAsync(
            connection,
            npgsqlTransaction,
            parsedCorpus.Verses.Count,
            cancellationToken);

        await CopyVersesAsync(
            connection,
            versionId,
            parsedCorpus.Verses,
            verseIds,
            cancellationToken);
        await CopyAnnotationsAsync(
            connection,
            parsedCorpus.Verses,
            verseIds,
            cancellationToken);
        await CopySupplementalTextsAsync(
            connection,
            parsedCorpus.Verses,
            verseIds,
            cancellationToken);

        await VerifyPersistedCountsAsync(
            connection,
            npgsqlTransaction,
            versionId,
            parsedCorpus.Books.Count,
            parsedCorpus.Verses.Count,
            annotationCount,
            parsedCorpus.Verses.Sum(verse => (long)verse.SupplementalTexts.Count),
            cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE bible_corpus_versions SET is_active = FALSE WHERE edition_code = {request.Edition.Code.Value} AND is_active",
            cancellationToken);

        var approvedAt = timeProvider.GetUtcNow();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE bible_corpus_versions
            SET validation_status = 'approved', approved_at = {approvedAt}, is_active = TRUE
            WHERE id = {versionId.Value}
            """,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return CreateResult(
            versionId,
            importFingerprint,
            wasCreated: true,
            parsedCorpus,
            annotationCount,
            strongAttributeCount);
    }

    private static void ValidateImportPolicy(BibleCorpusImportRequest request)
    {
        if (!string.Equals(request.Edition.CanonCode, CanonCode, StringComparison.Ordinal)
            || request.ValidationEvidence.ExpectedBookCount != CanonicalBookCount)
        {
            throw new BibleCorpusImportException(
                "The current importer accepts only a validated Protestant 66-book corpus.");
        }
    }

    private static (long AnnotationCount, long StrongAttributeCount) ValidateParsedCorpus(
        BibleCorpusImportRequest request,
        BibleCorpusReadResult parsedCorpus)
    {
        var evidence = request.ValidationEvidence;
        var annotationCount = parsedCorpus.Verses.Sum(
            verse => (long)verse.WordAnnotations.Count);
        var strongAttributeCount = parsedCorpus.Verses
            .SelectMany(verse => verse.WordAnnotations)
            .LongCount(annotation => string.Equals(
                annotation.Name,
                "strong",
                StringComparison.OrdinalIgnoreCase));

        if (parsedCorpus.Books.Count != evidence.ExpectedBookCount
            || parsedCorpus.Verses.Count != evidence.ExpectedVerseCount
            || strongAttributeCount != evidence.ExpectedStrongAttributeCount)
        {
            throw new BibleCorpusImportException(
                "Parsed corpus counts do not match the approved validation evidence: "
                + $"books {parsedCorpus.Books.Count}/{evidence.ExpectedBookCount}, "
                + $"verses {parsedCorpus.Verses.Count}/{evidence.ExpectedVerseCount}, "
                + $"Strong attributes {strongAttributeCount}/{evidence.ExpectedStrongAttributeCount}.");
        }

        var importedBooks = parsedCorpus.Books.Select(book => book.BookCode).ToHashSet();
        if (importedBooks.Count != CanonicalBookCount
            || parsedCorpus.Books.Any(book =>
                !ProtestantBibleBookCatalog.TryGetOrdinal(book.BookCode, out var expectedOrdinal)
                || expectedOrdinal != book.BookOrdinal)
            || parsedCorpus.Verses.Any(verse => !importedBooks.Contains(verse.Reference.BookCode)))
        {
            throw new BibleCorpusImportException(
                "Parsed verses and the imported Protestant book catalog are inconsistent.");
        }

        if (parsedCorpus.Books.Any(book =>
            book.DisplayName.Length > 200
            || book.ShortName is { Length: > 100 }
            || book.SourceRelativePath.Length > 500))
        {
            throw new BibleCorpusImportException(
                "Parsed book metadata exceeds the canonical persistence limits.");
        }

        var references = new HashSet<BibleReference>();
        var verseOrdinals = new HashSet<(UsfmBookCode Book, int Chapter, int Ordinal)>();

        foreach (var verse in parsedCorpus.Verses)
        {
            if (verse.Reference.VerseLabel.Length > 32
                || verse.SourceRelativePath.Length > 500
                || !references.Add(verse.Reference)
                || !verseOrdinals.Add((
                    verse.Reference.BookCode,
                    verse.Reference.ChapterNumber,
                    verse.VerseOrdinal)))
            {
                throw new BibleCorpusImportException(
                    $"Verse {verse.Reference} violates canonical reference or persistence constraints.");
            }

            var annotationOrdinals = new HashSet<int>();
            foreach (var annotation in verse.WordAnnotations)
            {
                if (annotation.Marker.Length > 16
                    || annotation.Name.Length > 64
                    || !annotationOrdinals.Add(annotation.SourceOrdinal)
                    || annotation.CharacterOffset + (long)annotation.CharacterLength > verse.Text.Length)
                {
                    throw new BibleCorpusImportException(
                        $"Word annotation {annotation.SourceOrdinal} violates constraints for verse {verse.Reference}.");
                }
            }

            var supplementalOrdinals = new HashSet<int>();
            foreach (var supplementalText in verse.SupplementalTexts)
            {
                if (supplementalText.Marker.Length > 16
                    || !supplementalOrdinals.Add(supplementalText.SourceOrdinal)
                    || supplementalText.CharacterOffset is { } offset && offset > verse.Text.Length)
                {
                    throw new BibleCorpusImportException(
                        $"Supplemental text {supplementalText.SourceOrdinal} violates constraints for verse {verse.Reference}.");
                }
            }
        }

        return (annotationCount, strongAttributeCount);
    }

    private async Task AcquireEditionLockAsync(
        BibleEditionCode editionCode,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({editionCode.Value}, 0))",
            cancellationToken);
    }

    private async Task EnsureEditionAsync(
        BibleEditionImportDefinition definition,
        CancellationToken cancellationToken)
    {
        var edition = await dbContext.Set<BibleEditionEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Code == definition.Code, cancellationToken);

        if (edition is null)
        {
            dbContext.Set<BibleEditionEntity>().Add(
                new BibleEditionEntity
                {
                    Code = definition.Code,
                    DisplayName = definition.DisplayName,
                    LanguageTag = definition.LanguageTag,
                    CanonCode = definition.CanonCode
                });
            return;
        }

        if (!string.Equals(edition.DisplayName, definition.DisplayName, StringComparison.Ordinal)
            || !string.Equals(edition.LanguageTag, definition.LanguageTag, StringComparison.Ordinal)
            || !string.Equals(edition.CanonCode, definition.CanonCode, StringComparison.Ordinal))
        {
            throw new BibleCorpusImportException(
                $"Edition {definition.Code} already exists with different immutable metadata.");
        }
    }

    private static async Task<long[]> AllocateVerseIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int count,
        CancellationToken cancellationToken)
    {
        var ids = new long[count];
        await using var command = new NpgsqlCommand(
            "SELECT nextval(pg_get_serial_sequence('bible_verses', 'id')) FROM generate_series(1, @count)",
            connection,
            transaction);
        command.Parameters.AddWithValue("count", NpgsqlDbType.Integer, count);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            ids[index++] = reader.GetInt64(0);
        }

        if (index != count)
        {
            throw new BibleCorpusImportException(
                $"PostgreSQL allocated {index} verse identifiers; expected {count}.");
        }

        return ids;
    }

    private static async Task CopyVersesAsync(
        NpgsqlConnection connection,
        BibleCorpusVersionId versionId,
        IReadOnlyList<ParsedBibleVerse> verses,
        IReadOnlyList<long> verseIds,
        CancellationToken cancellationToken)
    {
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY bible_verses
                (id, corpus_version_id, usfm_book_code, chapter_number, verse_label,
                 verse_ordinal, text, source_relative_path, source_line)
            FROM STDIN (FORMAT BINARY)
            """,
            cancellationToken);
        writer.Timeout = BulkImportTimeout;

        for (var index = 0; index < verses.Count; index++)
        {
            var verse = verses[index];
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(verseIds[index], NpgsqlDbType.Bigint, cancellationToken);
            await writer.WriteAsync(versionId.Value, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(verse.Reference.BookCode.Value, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(verse.Reference.ChapterNumber, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(verse.Reference.VerseLabel, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(verse.VerseOrdinal, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(verse.Text, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(verse.SourceRelativePath, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(verse.SourceLine, NpgsqlDbType.Integer, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task CopyAnnotationsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<ParsedBibleVerse> verses,
        IReadOnlyList<long> verseIds,
        CancellationToken cancellationToken)
    {
        if (!verses.Any(verse => verse.WordAnnotations.Count > 0))
        {
            return;
        }

        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY bible_word_annotations
                (verse_id, source_ordinal, marker, attribute_name, attribute_value,
                 character_offset, character_length)
            FROM STDIN (FORMAT BINARY)
            """,
            cancellationToken);
        writer.Timeout = BulkImportTimeout;

        for (var index = 0; index < verses.Count; index++)
        {
            foreach (var annotation in verses[index].WordAnnotations)
            {
                await writer.StartRowAsync(cancellationToken);
                await writer.WriteAsync(verseIds[index], NpgsqlDbType.Bigint, cancellationToken);
                await writer.WriteAsync(annotation.SourceOrdinal, NpgsqlDbType.Integer, cancellationToken);
                await writer.WriteAsync(annotation.Marker, NpgsqlDbType.Varchar, cancellationToken);
                await writer.WriteAsync(annotation.Name, NpgsqlDbType.Varchar, cancellationToken);
                await writer.WriteAsync(annotation.Value, NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(annotation.CharacterOffset, NpgsqlDbType.Integer, cancellationToken);
                await writer.WriteAsync(annotation.CharacterLength, NpgsqlDbType.Integer, cancellationToken);
            }
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task CopySupplementalTextsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<ParsedBibleVerse> verses,
        IReadOnlyList<long> verseIds,
        CancellationToken cancellationToken)
    {
        if (!verses.Any(verse => verse.SupplementalTexts.Count > 0))
        {
            return;
        }

        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY bible_supplemental_texts
                (verse_id, source_ordinal, marker, text, placement, character_offset)
            FROM STDIN (FORMAT BINARY)
            """,
            cancellationToken);
        writer.Timeout = BulkImportTimeout;

        for (var index = 0; index < verses.Count; index++)
        {
            foreach (var supplementalText in verses[index].SupplementalTexts)
            {
                await writer.StartRowAsync(cancellationToken);
                await writer.WriteAsync(verseIds[index], NpgsqlDbType.Bigint, cancellationToken);
                await writer.WriteAsync(supplementalText.SourceOrdinal, NpgsqlDbType.Integer, cancellationToken);
                await writer.WriteAsync(supplementalText.Marker, NpgsqlDbType.Varchar, cancellationToken);
                await writer.WriteAsync(supplementalText.Text, NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(supplementalText.Placement.ToString(), NpgsqlDbType.Varchar, cancellationToken);

                if (supplementalText.CharacterOffset is { } offset)
                {
                    await writer.WriteAsync(offset, NpgsqlDbType.Integer, cancellationToken);
                }
                else
                {
                    await writer.WriteNullAsync(cancellationToken);
                }
            }
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task VerifyPersistedCountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        BibleCorpusVersionId versionId,
        int expectedBooks,
        int expectedVerses,
        long expectedAnnotations,
        long expectedSupplementalTexts,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                (SELECT COUNT(*) FROM bible_corpus_books WHERE corpus_version_id = @version_id),
                (SELECT COUNT(*) FROM bible_verses WHERE corpus_version_id = @version_id),
                (SELECT COUNT(*)
                 FROM bible_word_annotations annotation
                 JOIN bible_verses verse ON verse.id = annotation.verse_id
                 WHERE verse.corpus_version_id = @version_id),
                (SELECT COUNT(*)
                 FROM bible_supplemental_texts supplemental_text
                 JOIN bible_verses verse ON verse.id = supplemental_text.verse_id
                 WHERE verse.corpus_version_id = @version_id)
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("version_id", NpgsqlDbType.Uuid, versionId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new BibleCorpusImportException("PostgreSQL returned no persisted corpus counts.");
        }

        var books = reader.GetInt64(0);
        var verses = reader.GetInt64(1);
        var annotations = reader.GetInt64(2);
        var supplementalTexts = reader.GetInt64(3);
        if (books != expectedBooks
            || verses != expectedVerses
            || annotations != expectedAnnotations
            || supplementalTexts != expectedSupplementalTexts)
        {
            throw new BibleCorpusImportException(
                "Persisted corpus counts do not match the validated parsed corpus: "
                + $"books {books}/{expectedBooks}, verses {verses}/{expectedVerses}, "
                + $"word annotations {annotations}/{expectedAnnotations}, "
                + $"supplemental texts {supplementalTexts}/{expectedSupplementalTexts}.");
        }
    }

    private static BibleCorpusImportResult CreateResult(
        BibleCorpusVersionId versionId,
        Sha256Digest importFingerprint,
        bool wasCreated,
        BibleCorpusReadResult parsedCorpus,
        long annotationCount,
        long strongAttributeCount) =>
        new(
            versionId,
            importFingerprint,
            wasCreated,
            parsedCorpus.Books.Count,
            parsedCorpus.Verses.Count,
            annotationCount,
            strongAttributeCount);

    private static string ToDatabaseRole(BibleSourceArtifactRole role) =>
        role switch
        {
            BibleSourceArtifactRole.CanonicalUsfm => "canonical-usfm",
            BibleSourceArtifactRole.ValidationVpl => "validation-vpl",
            BibleSourceArtifactRole.ValidationReport => "validation-report",
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
}
