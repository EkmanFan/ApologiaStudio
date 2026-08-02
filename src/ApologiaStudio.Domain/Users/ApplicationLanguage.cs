namespace ApologiaStudio.Domain.Users;

public enum ApplicationLanguage
{
    French,
    English
}

public static class ApplicationLanguageExtensions
{
    public static string ToLanguageTag(
        this ApplicationLanguage language)
    {
        EnsureSupported(language);

        return language == ApplicationLanguage.French
            ? "fr"
            : "en";
    }

    public static bool TryParseLanguageTag(
        string? languageTag,
        out ApplicationLanguage language)
    {
        if (string.Equals(
                languageTag,
                "fr",
                StringComparison.OrdinalIgnoreCase))
        {
            language = ApplicationLanguage.French;
            return true;
        }

        if (string.Equals(
                languageTag,
                "en",
                StringComparison.OrdinalIgnoreCase))
        {
            language = ApplicationLanguage.English;
            return true;
        }

        language = default;
        return false;
    }

    public static void EnsureSupported(
        this ApplicationLanguage language,
        string? parameterName = null)
    {
        if (!Enum.IsDefined(language))
        {
            throw new ArgumentOutOfRangeException(
                parameterName ?? nameof(language),
                language,
                "Only French and English are supported.");
        }
    }
}
