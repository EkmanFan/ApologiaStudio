using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed class BibleCorpusReadRequest
{
    public BibleCorpusReadRequest(
        string sourceDirectory,
        IEnumerable<UsfmBookCode>? excludedBookCodes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);

        SourceDirectory = sourceDirectory.Trim();
        ExcludedBookCodes = excludedBookCodes?.ToHashSet()
            ?? new HashSet<UsfmBookCode>();
    }

    public string SourceDirectory { get; }

    public IReadOnlySet<UsfmBookCode> ExcludedBookCodes { get; }
}
