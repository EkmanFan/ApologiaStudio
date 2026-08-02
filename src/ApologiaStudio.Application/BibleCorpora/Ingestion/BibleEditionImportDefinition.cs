using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record BibleEditionImportDefinition
{
    public BibleEditionImportDefinition(
        BibleEditionCode code,
        string displayName,
        string languageTag,
        string canonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonCode);

        if (displayName.Trim().Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(displayName));
        }

        if (languageTag.Trim().Length > 35)
        {
            throw new ArgumentOutOfRangeException(nameof(languageTag));
        }

        if (canonCode.Trim().Length > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(canonCode));
        }

        Code = code;
        DisplayName = displayName.Trim();
        LanguageTag = languageTag.Trim();
        CanonCode = canonCode.Trim();
    }

    public BibleEditionCode Code { get; }

    public string DisplayName { get; }

    public string LanguageTag { get; }

    public string CanonCode { get; }
}
