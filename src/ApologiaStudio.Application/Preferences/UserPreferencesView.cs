using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Preferences;

public sealed record UserPreferencesView(
    ApplicationLanguage InterfaceLanguage,
    ApplicationLanguage? TheologicalLanguage,
    ComposerEnterBehavior EnterBehavior =
        UserPreferences.DefaultEnterBehavior)
{
    public ApplicationLanguage EffectiveTheologicalLanguage =>
        TheologicalLanguage ?? InterfaceLanguage;

    public static UserPreferencesView Default { get; } =
        new(
            UserPreferences.DefaultInterfaceLanguage,
            TheologicalLanguage: null,
            EnterBehavior: UserPreferences.DefaultEnterBehavior);

    public static UserPreferencesView From(
        UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return new UserPreferencesView(
            preferences.InterfaceLanguage,
            preferences.TheologicalLanguage,
            preferences.EnterBehavior);
    }
}
