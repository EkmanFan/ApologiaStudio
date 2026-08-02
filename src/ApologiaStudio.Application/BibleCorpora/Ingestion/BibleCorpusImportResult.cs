using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record BibleCorpusImportResult(
    BibleCorpusVersionId CorpusVersionId,
    Sha256Digest ImportFingerprint,
    bool WasCreated,
    int BookCount,
    int VerseCount,
    long WordAnnotationCount,
    long StrongAttributeCount);
