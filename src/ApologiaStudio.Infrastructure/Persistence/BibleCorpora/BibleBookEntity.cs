using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

internal sealed class BibleBookEntity
{
    public UsfmBookCode UsfmCode { get; set; }

    public string OsisCode { get; set; } = string.Empty;

    public int CanonicalOrder { get; set; }

    public string CanonCode { get; set; } = string.Empty;
}
