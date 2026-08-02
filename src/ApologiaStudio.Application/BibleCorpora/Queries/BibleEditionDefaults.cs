using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.BibleCorpora.Queries;

public static class BibleEditionDefaults
{
    public static BibleEditionCode For(
        ApplicationLanguage language)
    {
        language.EnsureSupported(
            nameof(language));

        return new BibleEditionCode(
            language == ApplicationLanguage.French
                ? "lsg1910"
                : "web-classic");
    }
}
