using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using static Microsoft.AspNetCore.Components.Web.RenderMode;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using ApologiaStudio.Web;
using ApologiaStudio.Web.Components;
using ApologiaStudio.Web.Components.Layout;
using ApologiaStudio.Application.Preferences;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Web.Components.Pages;

public partial class Settings
{
    private SettingsTab _activeTab = SettingsTab.Languages;
    private string _interfaceLanguageCode = "fr";
    private string _theologicalLanguageCode = string.Empty;
    private ComposerEnterBehavior _composerEnterBehavior =
        UserPreferences.DefaultEnterBehavior;
    private string _messageDateFormat =
        UserPreferences.DefaultMessageDateFormat;
    private string _messageTimeFormat =
        UserPreferences.DefaultMessageTimeFormat;
    private ThemeMode _themeMode = UserPreferences.DefaultThemeMode;
    private string _themeColor = UserPreferences.DefaultThemeColor;
    private string _darkPageColor = UserPreferences.DefaultDarkPageColor;
    private string _darkSurfaceColor = UserPreferences.DefaultDarkSurfaceColor;
    private ThemeMode _savedThemeMode = UserPreferences.DefaultThemeMode;
    private string _savedThemeColor = UserPreferences.DefaultThemeColor;
    private string _savedDarkPageColor = UserPreferences.DefaultDarkPageColor;
    private string _savedDarkSurfaceColor = UserPreferences.DefaultDarkSurfaceColor;
    private bool _applyLoadedTheme;
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isLoading = true;
    private bool _isSaving;

    private bool ActiveTabCanBeSaved =>
        _activeTab is SettingsTab.Languages or
            SettingsTab.Dates or
            SettingsTab.Behavior or
            SettingsTab.Themes;

    private bool IsDefaultTheme =>
        _themeMode == UserPreferences.DefaultThemeMode &&
        string.Equals(
            _themeColor,
            UserPreferences.DefaultThemeColor,
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            _darkPageColor,
            UserPreferences.DefaultDarkPageColor,
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            _darkSurfaceColor,
            UserPreferences.DefaultDarkSurfaceColor,
            StringComparison.OrdinalIgnoreCase);

    private string ThemePreviewClass =>
        _themeMode == ThemeMode.Dark
            ? "theme-preview dark"
            : "theme-preview light";

    private string ThemeToggleClass =>
        _themeMode == ThemeMode.Dark
            ? "theme-mode-toggle dark"
            : "theme-mode-toggle light";

    private string ThemePreviewStyle =>
        $"--preview-accent: {_themeColor}; " +
        $"--preview-on-accent: {ThemePreviewForeground}; " +
        $"--preview-page: {(_themeMode == ThemeMode.Dark ? _darkPageColor : "#FBFAF7")}; " +
        $"--preview-surface: {(_themeMode == ThemeMode.Dark ? _darkSurfaceColor : "#FFFFFF")}";

    private int DarkPageShade => ParseShade(_darkPageColor);

    private int DarkSurfaceShade => ParseShade(_darkSurfaceColor);

    private string ThemePreviewForeground
    {
        get
        {
            var red = Convert.ToInt32(_themeColor[1..3], 16);
            var green = Convert.ToInt32(_themeColor[3..5], 16);
            var blue = Convert.ToInt32(_themeColor[5..7], 16);
            var perceivedBrightness =
                (red * 299 + green * 587 + blue * 114) / 1000;

            return perceivedBrightness >= 150
                ? "#111411"
                : "#FFFFFF";
        }
    }

    private string SaveButtonTitle =>
        ActiveTabCanBeSaved
            ? Text("Enregistrer les préférences", "Save preferences")
            : Text(
                "Cette catégorie sera disponible prochainement.",
                "This category will be available soon.");

    private ApplicationLanguage SelectedInterfaceLanguage =>
        ApplicationLanguageExtensions.TryParseLanguageTag(
            _interfaceLanguageCode,
            out var language)
            ? language
            : ApplicationLanguage.French;

    private ApplicationLanguage EffectiveTheologicalLanguage =>
        ApplicationLanguageExtensions.TryParseLanguageTag(
            _theologicalLanguageCode,
            out var language)
            ? language
            : SelectedInterfaceLanguage;

    private string EffectiveTheologicalLanguageLabel =>
        EffectiveTheologicalLanguage == ApplicationLanguage.French
            ? "Français"
            : "English";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            var handler =
                scope.ServiceProvider.GetRequiredService<
                    GetUserPreferencesHandler>();
            var preferences = await handler.HandleAsync(
                CancellationToken.None);

            _interfaceLanguageCode =
                preferences.InterfaceLanguage.ToLanguageTag();
            _theologicalLanguageCode =
                preferences.TheologicalLanguage?.ToLanguageTag() ??
                string.Empty;
            _composerEnterBehavior =
                preferences.EnterBehavior;
            _messageDateFormat =
                preferences.MessageDateFormat;
            _messageTimeFormat =
                preferences.MessageTimeFormat;
            _themeMode = preferences.ThemeMode;
            _themeColor = preferences.ThemeColor;
            _darkPageColor = preferences.DarkPageColor;
            _darkSurfaceColor = preferences.DarkSurfaceColor;
            _savedThemeMode = preferences.ThemeMode;
            _savedThemeColor = preferences.ThemeColor;
            _savedDarkPageColor = preferences.DarkPageColor;
            _savedDarkSurfaceColor = preferences.DarkSurfaceColor;
            _applyLoadedTheme = true;
        }
        catch (Exception exception)
        {
            _errorMessage =
                Text(
                    "Les préférences n’ont pas pu être chargées : ",
                    "Preferences could not be loaded: ") +
                exception.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_applyLoadedTheme)
        {
            return;
        }

        _applyLoadedTheme = false;
        await ApplyThemeAsync(
            _savedThemeMode,
            _savedThemeColor,
            _savedDarkPageColor,
            _savedDarkSurfaceColor,
            persistInBrowser: true);
    }

    private void SelectTab(SettingsTab tab)
    {
        if (_activeTab == tab)
        {
            return;
        }

        _activeTab = tab;
    }

    private string TabClass(SettingsTab tab)
    {
        return _activeTab == tab
            ? "settings-tab active"
            : "settings-tab";
    }

    private async Task ToggleThemeModeAsync()
    {
        _themeMode = _themeMode == ThemeMode.Light
            ? ThemeMode.Dark
            : ThemeMode.Light;
        _statusMessage = null;
        _errorMessage = null;
        await ApplyThemeAsync(
            _themeMode,
            _themeColor,
            _darkPageColor,
            _darkSurfaceColor,
            persistInBrowser: false);
    }

    private async Task UpdateThemeColor(ChangeEventArgs args)
    {
        if (args.Value is not string value)
        {
            return;
        }

        try
        {
            _themeColor = UserPreferences.NormalizeThemeColor(value);
            _statusMessage = null;
            _errorMessage = null;
            await ApplyThemeAsync(
                _themeMode,
                _themeColor,
                _darkPageColor,
                _darkSurfaceColor,
                persistInBrowser: false);
        }
        catch (ArgumentException)
        {
            // Native color inputs only emit valid #RRGGBB values.
        }
    }

    private async Task UpdateDarkPageShade(ChangeEventArgs args)
    {
        if (!TryCreateShade(args.Value, out var shade))
        {
            return;
        }

        _darkPageColor = shade;
        _statusMessage = null;
        _errorMessage = null;
        await ApplyThemeAsync(
            _themeMode,
            _themeColor,
            _darkPageColor,
            _darkSurfaceColor,
            persistInBrowser: false);
    }

    private async Task UpdateDarkSurfaceShade(ChangeEventArgs args)
    {
        if (!TryCreateShade(args.Value, out var shade))
        {
            return;
        }

        _darkSurfaceColor = shade;
        _statusMessage = null;
        _errorMessage = null;
        await ApplyThemeAsync(
            _themeMode,
            _themeColor,
            _darkPageColor,
            _darkSurfaceColor,
            persistInBrowser: false);
    }

    private async Task ResetThemeAsync()
    {
        _themeMode = UserPreferences.DefaultThemeMode;
        _themeColor = UserPreferences.DefaultThemeColor;
        _darkPageColor = UserPreferences.DefaultDarkPageColor;
        _darkSurfaceColor = UserPreferences.DefaultDarkSurfaceColor;
        _statusMessage = null;
        _errorMessage = null;
        await ApplyThemeAsync(
            _themeMode,
            _themeColor,
            _darkPageColor,
            _darkSurfaceColor,
            persistInBrowser: false);
    }

    private async Task CloseAsync()
    {
        try
        {
            await ApplyThemeAsync(
                _savedThemeMode,
                _savedThemeColor,
                _savedDarkPageColor,
                _savedDarkSurfaceColor,
                persistInBrowser: true);
        }
        finally
        {
            Navigation.NavigateTo("/");
        }
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        _statusMessage = null;
        _errorMessage = null;

        try
        {
            ApplicationLanguage? theologicalLanguage =
                string.IsNullOrWhiteSpace(
                    _theologicalLanguageCode)
                    ? null
                    : EffectiveTheologicalLanguage;

            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            var handler =
                scope.ServiceProvider.GetRequiredService<
                    UpdateUserPreferencesHandler>();

            await handler.HandleAsync(
                new UpdateUserPreferencesCommand(
                    InterfaceLanguage: SelectedInterfaceLanguage,
                    TheologicalLanguage: theologicalLanguage,
                    EnterBehavior: _composerEnterBehavior,
                    MessageDateFormat: _messageDateFormat,
                    MessageTimeFormat: _messageTimeFormat,
                    ThemeMode: _themeMode,
                    ThemeColor: _themeColor,
                    DarkPageColor: _darkPageColor,
                    DarkSurfaceColor: _darkSurfaceColor),
                CancellationToken.None);

            _savedThemeMode = _themeMode;
            _savedThemeColor = _themeColor;
            _savedDarkPageColor = _darkPageColor;
            _savedDarkSurfaceColor = _darkSurfaceColor;

            await ApplyThemeAsync(
                _savedThemeMode,
                _savedThemeColor,
                _savedDarkPageColor,
                _savedDarkSurfaceColor,
                persistInBrowser: true);

            _statusMessage =
                Text(
                    "Préférences enregistrées.",
                    "Preferences saved.");
        }
        catch (Exception exception)
        {
            _errorMessage =
                Text(
                    "Les préférences n’ont pas pu être enregistrées : ",
                    "Preferences could not be saved: ") +
                exception.Message;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private ValueTask ApplyThemeAsync(
        ThemeMode mode,
        string color,
        string darkPageColor,
        string darkSurfaceColor,
        bool persistInBrowser)
    {
        return JavaScript.InvokeVoidAsync(
            "apologiaStudio.applyTheme",
            mode.ToString().ToLowerInvariant(),
            color,
            darkPageColor,
            darkSurfaceColor,
            persistInBrowser);
    }

    private static int ParseShade(string color)
    {
        return Convert.ToInt32(color[1..3], 16);
    }

    private static bool TryCreateShade(
        object? value,
        out string shade)
    {
        if (int.TryParse(
                value?.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var channel))
        {
            channel = Math.Clamp(
                channel,
                UserPreferences.MinimumDarkShade,
                UserPreferences.MaximumDarkShade);
            shade = $"#{channel:X2}{channel:X2}{channel:X2}";
            return true;
        }

        shade = UserPreferences.DefaultDarkPageColor;
        return false;
    }

    private string Text(
        string french,
        string english)
    {
        return SelectedInterfaceLanguage ==
                ApplicationLanguage.English
            ? english
            : french;
    }

    private enum SettingsTab
    {
        Languages,
        Dates,
        Behavior,
        Themes,
        LandingPage
    }
}
