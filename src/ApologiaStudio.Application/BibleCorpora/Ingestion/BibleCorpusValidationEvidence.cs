namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record BibleCorpusValidationEvidence
{
    public BibleCorpusValidationEvidence(
        int expectedBookCount,
        int expectedVerseCount,
        long expectedStrongAttributeCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedBookCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedVerseCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedStrongAttributeCount);

        ExpectedBookCount = expectedBookCount;
        ExpectedVerseCount = expectedVerseCount;
        ExpectedStrongAttributeCount = expectedStrongAttributeCount;
    }

    public int ExpectedBookCount { get; }

    public int ExpectedVerseCount { get; }

    public long ExpectedStrongAttributeCount { get; }
}
