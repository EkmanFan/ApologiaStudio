using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Preferences;

public sealed record UserPreferencesView(
    ApplicationLanguage InterfaceLanguage,
    ApplicationLanguage? TheologicalLanguage,
    ComposerEnterBehavior EnterBehavior =
        UserPreferences.DefaultEnterBehavior,
    string MessageDateFormat =
        UserPreferences.DefaultMessageDateFormat,
    string MessageTimeFormat =
        UserPreferences.DefaultMessageTimeFormat,
    ThemeMode ThemeMode = UserPreferences.DefaultThemeMode,
    string ThemeColor = UserPreferences.DefaultThemeColor,
    string DarkPageColor = UserPreferences.DefaultDarkPageColor,
    string DarkSurfaceColor = UserPreferences.DefaultDarkSurfaceColor)
{
    public ApplicationLanguage EffectiveTheologicalLanguage =>
        TheologicalLanguage ?? InterfaceLanguage;

    public static UserPreferencesView Default { get; } =
        new(
            UserPreferences.DefaultInterfaceLanguage,
            TheologicalLanguage: null,
            EnterBehavior: UserPreferences.DefaultEnterBehavior,
            MessageDateFormat: UserPreferences.DefaultMessageDateFormat,
            MessageTimeFormat: UserPreferences.DefaultMessageTimeFormat,
            ThemeMode: UserPreferences.DefaultThemeMode,
            ThemeColor: UserPreferences.DefaultThemeColor,
            DarkPageColor: UserPreferences.DefaultDarkPageColor,
            DarkSurfaceColor: UserPreferences.DefaultDarkSurfaceColor);

    public static UserPreferencesView From(
        UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return new UserPreferencesView(
            preferences.InterfaceLanguage,
            preferences.TheologicalLanguage,
            preferences.EnterBehavior,
            preferences.MessageDateFormat,
            preferences.MessageTimeFormat,
            preferences.ThemeMode,
            preferences.ThemeColor,
            preferences.DarkPageColor,
            preferences.DarkSurfaceColor);
    }
}
