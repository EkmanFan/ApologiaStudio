using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

internal sealed class BibleEditionEntity
{
    public BibleEditionCode Code { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string LanguageTag { get; set; } = string.Empty;

    public string CanonCode { get; set; } = string.Empty;
}
