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
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.AiRuntime.Settings;

namespace ApologiaStudio.Web.Components.Pages;

public partial class OllamaRuntimeSettingsPanel
{
    private IReadOnlyList<OllamaLocalModel> _models =
        Array.Empty<OllamaLocalModel>();
    private IReadOnlyDictionary<Guid, string> _legacyAgentModels =
        new Dictionary<Guid, string>();

    private string _baseAddress = string.Empty;
    private string _routingModel = string.Empty;
    private string _defaultAgentModel = string.Empty;
    private string _keepAlive = string.Empty;
    private string? _runtimeStatusMessage;
    private string? _modelCatalogMessage;
    private int _routingTimeoutSeconds;
    private int _generationTimeoutSeconds;
    private int _maximumHistoryMessages;
    private int _maximumHistoryCharacters;
    private int _maximumOutputTokens;
    private int _modelLoadVersion;
    private bool _isLoadingSettings = true;
    private bool _isLoadingModels;
    private bool _isSavingRuntime;
    private bool _runtimeStatusIsError;
    private bool _modelCatalogIsError;

    private bool AreRuntimeModelsAvailable =>
        IsModelAvailable(_routingModel) &&
        IsModelAvailable(_defaultAgentModel);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();
            var handler =
                scope.ServiceProvider.GetRequiredService<
                    GetAiRuntimeSettingsHandler>();

            var runtimeSettings =
                await handler.HandleAsync(CancellationToken.None);

            LoadRuntime(runtimeSettings);
            await LoadModelsAsync(showSuccess: false);
        }
        catch (Exception exception)
        {
            _runtimeStatusIsError = true;
            _runtimeStatusMessage =
                "Les paramètres d’IA n’ont pas pu être chargés : " +
                exception.Message;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void SetRoutingModel(string value)
    {
        _routingModel = value;
    }

    private void SetDefaultAgentModel(string value)
    {
        _defaultAgentModel = value;
    }

    private async Task HandleBaseAddressChangedAsync(
        ChangeEventArgs eventArgs)
    {
        _baseAddress =
            eventArgs.Value?.ToString()?.Trim()
            ?? string.Empty;

        _runtimeStatusMessage = null;
        await LoadModelsAsync(showSuccess: false);
    }

    private Task RefreshModelsAsync()
    {
        return LoadModelsAsync(showSuccess: true);
    }

    private async Task LoadModelsAsync(bool showSuccess)
    {
        var loadVersion = ++_modelLoadVersion;

        _isLoadingModels = true;
        _modelCatalogMessage = null;
        _modelCatalogIsError = false;

        try
        {
            var baseAddress =
                AiRuntimeSettingsValidator.NormalizeBaseAddress(
                    _baseAddress);
            var models =
                await ModelCatalogClient.ListLocalModelsAsync(
                    baseAddress);

            if (loadVersion != _modelLoadVersion)
            {
                return;
            }

            _models = models;
            if (_models.Count == 0)
            {
                _modelCatalogIsError = true;
                _modelCatalogMessage =
                    "Aucun modèle local n’est installé sur ce serveur Ollama.";
                return;
            }

            var unavailable = GetUnavailableSelections();
            if (unavailable.Count > 0)
            {
                _modelCatalogIsError = true;
                _modelCatalogMessage =
                    "Modèle(s) configuré(s) indisponible(s) : " +
                    string.Join(", ", unavailable) +
                    ". Sélectionnez un modèle installé localement.";
            }
            else if (showSuccess)
            {
                _modelCatalogMessage =
                    $"{_models.Count} modèle(s) local(aux) chargé(s).";
            }
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  HttpRequestException or
                  InvalidOperationException or
                  TaskCanceledException)
        {
            _models = Array.Empty<OllamaLocalModel>();
            _modelCatalogIsError = true;
            _modelCatalogMessage =
                "Impossible de charger les modèles locaux : " +
                exception.Message;
        }
        finally
        {
            if (loadVersion == _modelLoadVersion)
            {
                _isLoadingModels = false;
            }
        }
    }

    private async Task SaveRuntimeAsync()
    {
        if (_isSavingRuntime)
        {
            return;
        }

        _isSavingRuntime = true;
        _runtimeStatusMessage = null;
        _runtimeStatusIsError = false;

        try
        {
            if (!AreRuntimeModelsAvailable)
            {
                throw new InvalidOperationException(
                    "Les modèles du runtime doivent être installés sur " +
                    "le serveur Ollama indiqué.");
            }

            var assignments = _legacyAgentModels
                .Select(
                    assignment =>
                        new AgentModelAssignmentInput(
                            assignment.Key,
                            assignment.Value))
                .ToArray();

            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();
            var handler =
                scope.ServiceProvider.GetRequiredService<
                    UpdateAiRuntimeSettingsHandler>();

            var settings =
                await handler.HandleAsync(
                    new UpdateAiRuntimeSettingsCommand(
                        _baseAddress,
                        _routingModel,
                        _defaultAgentModel,
                        _routingTimeoutSeconds,
                        _generationTimeoutSeconds,
                        _keepAlive,
                        _maximumHistoryMessages,
                        _maximumHistoryCharacters,
                        _maximumOutputTokens,
                        assignments),
                    CancellationToken.None);

            LoadRuntime(settings);
            _runtimeStatusMessage =
                "Les paramètres du runtime ont été enregistrés dans PostgreSQL.";
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  InvalidOperationException or
                  IOException or
                  UnauthorizedAccessException)
        {
            _runtimeStatusIsError = true;
            _runtimeStatusMessage = exception.Message;
        }
        finally
        {
            _isSavingRuntime = false;
        }
    }

    private void LoadRuntime(AiRuntimeSettingsSnapshot settings)
    {
        _baseAddress = settings.BaseAddress;
        _routingModel = settings.RoutingModel;
        _defaultAgentModel = settings.DefaultAgentModel;
        _routingTimeoutSeconds = settings.RoutingTimeoutSeconds;
        _generationTimeoutSeconds = settings.GenerationTimeoutSeconds;
        _keepAlive = settings.KeepAlive;
        _maximumHistoryMessages = settings.MaximumHistoryMessages;
        _maximumHistoryCharacters = settings.MaximumHistoryCharacters;
        _maximumOutputTokens = settings.MaximumOutputTokens;
        _legacyAgentModels =
            new Dictionary<Guid, string>(settings.AgentModels);
    }

    private bool IsModelAvailable(string model)
    {
        return _models.Any(
            candidate =>
                string.Equals(
                    candidate.Name,
                    model,
                    StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<string> GetUnavailableSelections()
    {
        return new[]
            {
                _routingModel,
                _defaultAgentModel
            }
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Where(model => !IsModelAvailable(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
