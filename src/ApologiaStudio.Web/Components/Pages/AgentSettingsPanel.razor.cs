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
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Application.AiRuntime.Settings;

namespace ApologiaStudio.Web.Components.Pages;

public sealed record AgentCreationActionState(
    int ActiveAgentCount,
    int MaximumActiveAgents,
    bool HasDraft,
    bool IsBusy,
    bool IsLoading)
{
    public static AgentCreationActionState Loading { get; } =
        new(0, AgentSettingsPolicy.MaximumActiveAgents, false, false, true);

    public bool CanAddAgent =>
        !IsLoading &&
        !IsBusy &&
        !HasDraft &&
        ActiveAgentCount < MaximumActiveAgents;
}

public partial class AgentSettingsPanel
{
    [Parameter]
    public int ActivationVersion { get; set; }

    [Parameter]
    public EventCallback<AgentCreationActionState>
        CreationActionStateChanged { get; set; }

    private IReadOnlyList<OllamaLocalModel> _models =
        Array.Empty<OllamaLocalModel>();
    private List<AgentEditor> _agentEditors = [];
    private readonly HashSet<Guid> _savingAgentIds = [];

    private string _baseAddress = string.Empty;
    private string _defaultAgentModel = string.Empty;
    private string? _loadStatusMessage;
    private string? _modelCatalogMessage;
    private int _modelLoadVersion;
    private int _lastActivationVersion;
    private bool _initialized;
    private bool _isLoadingSettings = true;
    private bool _isLoadingModels;
    private bool _modelCatalogIsError;

    private int ActiveAgentCount =>
        _agentEditors.Count(editor => !editor.IsNew);

    private bool CanAddAgent =>
        ActiveAgentCount < AgentSettingsPolicy.MaximumActiveAgents &&
        !_agentEditors.Any(editor => editor.IsNew) &&
        _savingAgentIds.Count == 0;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();
            var runtimeHandler =
                scope.ServiceProvider.GetRequiredService<
                    GetAiRuntimeSettingsHandler>();
            var agentHandler =
                scope.ServiceProvider.GetRequiredService<
                    GetAgentSettingsHandler>();

            var runtimeSettings =
                await runtimeHandler.HandleAsync(CancellationToken.None);
            var agentSettings =
                await agentHandler.HandleAsync(CancellationToken.None);

            LoadRuntimeContext(runtimeSettings);
            LoadAgents(agentSettings);
            await LoadModelsAsync(showSuccess: false);
        }
        catch (Exception exception)
        {
            _loadStatusMessage =
                "Les profils d’agents n’ont pas pu être chargés : " +
                exception.Message;
        }
        finally
        {
            _lastActivationVersion = ActivationVersion;
            _initialized = true;
            _isLoadingSettings = false;
            await NotifyCreationActionStateAsync();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized ||
            ActivationVersion == _lastActivationVersion)
        {
            return;
        }

        _lastActivationVersion = ActivationVersion;
        await RefreshRuntimeContextAsync();
    }

    private async Task RefreshRuntimeContextAsync()
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

            LoadRuntimeContext(runtimeSettings);
            await LoadModelsAsync(showSuccess: false);
        }
        catch (Exception exception)
        {
            _modelCatalogIsError = true;
            _modelCatalogMessage =
                "Le contexte Ollama n’a pas pu être actualisé : " +
                exception.Message;
        }
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
                    ". Vérifiez l’onglet IA ou le modèle de l’agent.";
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

    private async Task SaveAgentAsync(AgentEditor editor)
    {
        var operationId = editor.AgentId.Value;
        if (!_savingAgentIds.Add(operationId))
        {
            return;
        }

        editor.StatusMessage = null;
        editor.StatusIsError = false;

        try
        {
            ValidateSelectedModel(editor);

            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();

            AgentSettingsSnapshot saved;
            if (editor.IsNew)
            {
                var handler =
                    scope.ServiceProvider.GetRequiredService<
                        CreateAgentSettingsHandler>();
                saved = await handler.HandleAsync(
                    new CreateAgentSettingsCommand(
                        editor.DisplayName,
                        editor.Avatar,
                        editor.BubbleColor,
                        NormalizeOptionalModel(editor.Model),
                        editor.SystemPrompt,
                        editor.RoutingDescription),
                    CancellationToken.None);
            }
            else
            {
                var handler =
                    scope.ServiceProvider.GetRequiredService<
                        UpdateAgentSettingsHandler>();
                saved = await handler.HandleAsync(
                    new UpdateAgentSettingsCommand(
                        editor.AgentId,
                        editor.DisplayName,
                        editor.Avatar,
                        editor.BubbleColor,
                        NormalizeOptionalModel(editor.Model),
                        editor.SystemPrompt,
                        editor.RoutingDescription),
                    CancellationToken.None);
            }

            editor.Load(saved);
            editor.StatusMessage = editor.IsBuiltIn
                ? "Profil enregistré. Il sera utilisé dès le prochain tour."
                : "Agent enregistré et disponible immédiatement pour le routage.";
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  InvalidOperationException or
                  IOException or
                  UnauthorizedAccessException)
        {
            editor.StatusIsError = true;
            editor.StatusMessage = exception.Message;
        }
        finally
        {
            _savingAgentIds.Remove(operationId);
            await NotifyCreationActionStateAsync();
        }
    }

    private async Task DeleteAgentAsync(AgentEditor editor)
    {
        if (editor.IsBuiltIn || editor.IsNew)
        {
            return;
        }

        var operationId = editor.AgentId.Value;
        if (!_savingAgentIds.Add(operationId))
        {
            return;
        }

        editor.StatusMessage = null;
        editor.StatusIsError = false;

        try
        {
            await using var scope =
                ServiceScopeFactory.CreateAsyncScope();
            var handler =
                scope.ServiceProvider.GetRequiredService<
                    DeleteAgentSettingsHandler>();

            await handler.HandleAsync(
                editor.AgentId,
                CancellationToken.None);

            _agentEditors.Remove(editor);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  InvalidOperationException or
                  IOException or
                  UnauthorizedAccessException)
        {
            editor.StatusIsError = true;
            editor.StatusMessage = exception.Message;
            editor.ConfirmDelete = false;
        }
        finally
        {
            _savingAgentIds.Remove(operationId);
            await NotifyCreationActionStateAsync();
        }
    }

    public async Task RequestAddAgentAsync()
    {
        if (!CanAddAgent)
        {
            return;
        }

        var temporaryId = ApologiaStudio.Domain.Agents.AgentId.New();
        _agentEditors.Add(
            AgentEditor.NewDraft(temporaryId));
        await NotifyCreationActionStateAsync();
    }

    private async Task CancelNewAgent(AgentEditor editor)
    {
        if (!editor.IsNew ||
            _savingAgentIds.Contains(editor.AgentId.Value))
        {
            return;
        }

        _agentEditors.Remove(editor);
        await NotifyCreationActionStateAsync();
    }

    private Task NotifyCreationActionStateAsync() =>
        CreationActionStateChanged.InvokeAsync(
            new AgentCreationActionState(
                ActiveAgentCount,
                AgentSettingsPolicy.MaximumActiveAgents,
                _agentEditors.Any(editor => editor.IsNew),
                _savingAgentIds.Count > 0,
                _isLoadingSettings));

    private void ValidateSelectedModel(AgentEditor editor)
    {
        if (_models.Count > 0 &&
            !IsOptionalModelAvailable(editor.Model))
        {
            throw new InvalidOperationException(
                "Le modèle sélectionné pour cet agent n’est pas installé " +
                "sur le serveur Ollama indiqué.");
        }
    }

    private static string? NormalizeOptionalModel(string? model)
    {
        return string.IsNullOrWhiteSpace(model)
            ? null
            : model;
    }

    private void ResetAgentToDefaults(AgentEditor editor)
    {
        var defaults = AgentDefaults.Get(editor.AgentId);
        editor.DisplayName = defaults.Agent.DisplayName;
        editor.Avatar = defaults.Avatar;
        editor.BubbleColor = defaults.BubbleColor;
        editor.Model = string.Empty;
        editor.SystemPrompt = defaults.Prompt.SystemPrompt;
        editor.RoutingDescription = defaults.RoutingDescription;
        editor.StatusIsError = false;
        editor.StatusMessage =
            "Valeurs par défaut restaurées localement. Enregistrez pour les appliquer.";
    }

    private void LoadRuntimeContext(AiRuntimeSettingsSnapshot settings)
    {
        _baseAddress = settings.BaseAddress;
        _defaultAgentModel = settings.DefaultAgentModel;
    }

    private void LoadAgents(
        IReadOnlyList<AgentSettingsSnapshot> persistedSettings)
    {
        _agentEditors = persistedSettings
            .Where(settings => settings.IsEnabled)
            .OrderByDescending(settings => settings.IsBuiltIn)
            .ThenBy(settings => settings.DisplayName)
            .Select(AgentEditor.From)
            .ToList();
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

    private bool IsOptionalModelAvailable(string? model)
    {
        return string.IsNullOrWhiteSpace(model) ||
               IsModelAvailable(model);
    }

    private IReadOnlyList<string> GetUnavailableSelections()
    {
        return new[] { _defaultAgentModel }
            .Concat(_agentEditors.Select(editor => editor.Model))
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Where(model => !IsModelAvailable(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void SetAgentColor(
        AgentEditor editor,
        ChangeEventArgs eventArgs)
    {
        var value = eventArgs.Value?.ToString();
        if (!string.IsNullOrWhiteSpace(value))
        {
            editor.BubbleColor = value.ToUpperInvariant();
        }
    }

    private static string NormalizeColorForPicker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length != 7 ||
            value[0] != '#' ||
            !int.TryParse(
                value.AsSpan(1),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out _))
        {
            return "#F3F2EE";
        }

        return value;
    }

    private static string GetAgentPreviewStyle(AgentEditor editor)
    {
        var color = NormalizeColorForPicker(editor.BubbleColor);
        return $"--agent-preview-color:{color};";
    }

    private sealed class AgentEditor(
        ApologiaStudio.Domain.Agents.AgentId agentId,
        string slug,
        string displayName,
        string avatar,
        string bubbleColor,
        string model,
        string systemPrompt,
        string routingDescription,
        bool isBuiltIn,
        bool isNew)
    {
        public ApologiaStudio.Domain.Agents.AgentId AgentId { get; private set; } = agentId;
        public string Slug { get; private set; } = slug;
        public string DisplayName { get; set; } = displayName;
        public string Avatar { get; set; } = avatar;
        public string BubbleColor { get; set; } = bubbleColor;
        public string Model { get; set; } = model;
        public string SystemPrompt { get; set; } = systemPrompt;
        public string RoutingDescription { get; set; } = routingDescription;
        public bool IsBuiltIn { get; private set; } = isBuiltIn;
        public bool IsNew { get; private set; } = isNew;
        public bool ConfirmDelete { get; set; }
        public string? StatusMessage { get; set; }
        public bool StatusIsError { get; set; }

        public static AgentEditor From(AgentSettingsSnapshot settings)
        {
            return new AgentEditor(
                settings.AgentId,
                settings.Slug,
                settings.DisplayName,
                settings.Avatar,
                settings.BubbleColor,
                settings.Model ?? string.Empty,
                settings.SystemPrompt,
                settings.RoutingDescription,
                settings.IsBuiltIn,
                isNew: false);
        }

        public static AgentEditor NewDraft(
            ApologiaStudio.Domain.Agents.AgentId temporaryId)
        {
            return new AgentEditor(
                temporaryId,
                string.Empty,
                "Nouvel agent",
                "🤖",
                "#EAF0F3",
                string.Empty,
                "You are a specialized AI assistant in ApologiaStudio. " +
                "Follow the user's request while remaining within your configured area of expertise.",
                string.Empty,
                isBuiltIn: false,
                isNew: true);
        }

        public void Load(AgentSettingsSnapshot settings)
        {
            AgentId = settings.AgentId;
            Slug = settings.Slug;
            DisplayName = settings.DisplayName;
            Avatar = settings.Avatar;
            BubbleColor = settings.BubbleColor;
            Model = settings.Model ?? string.Empty;
            SystemPrompt = settings.SystemPrompt;
            RoutingDescription = settings.RoutingDescription;
            IsBuiltIn = settings.IsBuiltIn;
            IsNew = false;
            ConfirmDelete = false;
        }
    }
}
