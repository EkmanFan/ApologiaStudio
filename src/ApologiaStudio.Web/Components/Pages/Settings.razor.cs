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
    private string? _statusMessage;
    private string? _errorMessage;
    private bool _isLoading = true;
    private bool _isSaving;

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
                    SelectedInterfaceLanguage,
                    theologicalLanguage,
                    _composerEnterBehavior,
                    _messageDateFormat,
                    _messageTimeFormat),
                CancellationToken.None);

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
