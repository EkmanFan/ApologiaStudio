using System.Text.Json;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Web.AiRuntime;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Application.BibleCorpora.Reader;
using ApologiaStudio.Application.Conversations.CreateConversation;
using ApologiaStudio.Application.Conversations.DeleteConversation;
using ApologiaStudio.Application.Conversations.GetConversation;
using ApologiaStudio.Application.Conversations.ListConversations;
using ApologiaStudio.Application.Conversations.MoveConversation;
using ApologiaStudio.Application.Conversations.RenameConversation;
using ApologiaStudio.Application.Conversations.RestoreConversation;
using ApologiaStudio.Application.Conversations.SendMessage;
using ApologiaStudio.Application.Navigation.ReorderPinnedItems;
using ApologiaStudio.Application.Navigation.ReorderProjects;
using ApologiaStudio.Application.Navigation.SetSidebarPin;
using ApologiaStudio.Application.Preferences;
using ApologiaStudio.Application.Navigation.GetSidebarNavigation;
using ApologiaStudio.Application.Projects.CreateProject;
using ApologiaStudio.Application.Projects.DeleteProject;
using ApologiaStudio.Application.Projects.RenameProject;
using ApologiaStudio.Infrastructure;
using ApologiaStudio.Web;
using ApologiaStudio.Web.Components;
using ApologiaStudio.Web.DocumentManager;
using ApologiaStudio.Web.Endpoints;
using ApologiaStudio.Web.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// APOLOGIA-DATABASE-BACKED-AI-RUNTIME-SETTINGS
var aiRuntimeDefaults =
    CreateAiRuntimeDefaults(
        builder.Configuration);

builder.Services.AddApologiaStudioWebServices(
    builder.Configuration,
    aiRuntimeDefaults);

var app = builder.Build();

await app.Services
    .GetRequiredService<IdentityBootstrapper>()
    .InitializeAsync(app.Lifetime.ApplicationStopping);

await InitializeAiRuntimeSettingsAsync(
    app,
    aiRuntimeDefaults);
await InitializeAgentSettingsAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapBibleCorpusEndpoints();
app.MapDocumentManagerNotificationEndpoint();
app.MapIdentitySessionEndpoints();

app.Run();

static AiRuntimeSettingsDefaults CreateAiRuntimeDefaults(
    IConfiguration configuration)
{
    var baseAddress =
        configuration["Ollama:BaseUrl"]
        ?? throw new InvalidOperationException(
            "Ollama:BaseUrl is required.");

    var routingModel =
        configuration["Ollama:RoutingModel"]
        ?? throw new InvalidOperationException(
            "Ollama:RoutingModel is required.");

    var defaultAgentModel =
        configuration["Ollama:ResponseModel"]
        ?? routingModel;

    return new AiRuntimeSettingsDefaults(
        baseAddress,
        routingModel,
        defaultAgentModel,
        configuration.GetValue<int?>(
            "Ollama:TimeoutSeconds") ?? 60,
        configuration.GetValue<int?>(
            "Ollama:GenerationTimeoutSeconds") ?? 180,
        configuration["Ollama:KeepAlive"] ?? "10m",
        configuration.GetValue<int?>(
            "Ollama:MaximumHistoryMessages") ?? 24,
        configuration.GetValue<int?>(
            "Ollama:MaximumHistoryCharacters") ?? 24_000,
        configuration.GetValue<int?>(
            "Ollama:MaximumOutputTokens") ?? 1_200);
}

static async Task InitializeAiRuntimeSettingsAsync(
    WebApplication app,
    AiRuntimeSettingsDefaults defaults)
{
    var legacyPathText =
        app.Configuration["Ollama:RoutingSettingsPath"];

    var legacyPath =
        string.IsNullOrWhiteSpace(legacyPathText)
            ? null
            : Path.IsPathRooted(legacyPathText)
                ? legacyPathText
                : Path.Combine(
                    app.Environment.ContentRootPath,
                    legacyPathText);

    await using var scope =
        app.Services.CreateAsyncScope();

    var settingsStore =
        scope.ServiceProvider.GetRequiredService<
            IAiRuntimeSettingsStore>();

    if (await settingsStore.GetAsync(CancellationToken.None)
        is not null)
    {
        TryDeleteLegacySettings(legacyPath);
        return;
    }

    var baseAddress = defaults.BaseAddress;
    var routingModel = defaults.RoutingModel;
    var defaultAgentModel = defaults.DefaultAgentModel;
    var routingTimeoutSeconds = defaults.RoutingTimeoutSeconds;
    var keepAlive = defaults.KeepAlive;

    if (legacyPath is not null && File.Exists(legacyPath))
    {
        await using var stream = File.OpenRead(legacyPath);
        using var document =
            await JsonDocument.ParseAsync(stream);

        var root = document.RootElement;

        if (root.TryGetProperty("baseAddress", out var addressProperty) &&
            addressProperty.ValueKind == JsonValueKind.String)
        {
            baseAddress =
                addressProperty.GetString() ?? baseAddress;
        }

        if (root.TryGetProperty("model", out var modelProperty) &&
            modelProperty.ValueKind == JsonValueKind.String)
        {
            var migratedModel = modelProperty.GetString();

            if (!string.IsNullOrWhiteSpace(migratedModel))
            {
                routingModel = migratedModel;
                defaultAgentModel = migratedModel;
            }
        }

        if (root.TryGetProperty(
                "requestTimeoutSeconds",
                out var timeoutProperty) &&
            timeoutProperty.TryGetInt32(out var migratedTimeout))
        {
            routingTimeoutSeconds = migratedTimeout;
        }

        if (root.TryGetProperty("keepAlive", out var keepAliveProperty) &&
            keepAliveProperty.ValueKind == JsonValueKind.String)
        {
            keepAlive =
                keepAliveProperty.GetString() ?? keepAlive;
        }
    }

    var initialCommand =
        new UpdateAiRuntimeSettingsCommand(
            baseAddress,
            routingModel,
            defaultAgentModel,
            routingTimeoutSeconds,
            defaults.GenerationTimeoutSeconds,
            keepAlive,
            defaults.MaximumHistoryMessages,
            defaults.MaximumHistoryCharacters,
            defaults.MaximumOutputTokens,
            Array.Empty<AgentModelAssignmentInput>());

    var initialSettings =
        AiRuntimeSettingsValidator.Normalize(
            initialCommand,
            TimeProvider.System.GetUtcNow());

    var initializer =
        scope.ServiceProvider.GetRequiredService<
            InitializeAiRuntimeSettingsHandler>();

    await initializer.HandleAsync(
        initialSettings,
        CancellationToken.None);

    TryDeleteLegacySettings(legacyPath);
}

static async Task InitializeAgentSettingsAsync(WebApplication app)
{
    await using var scope =
        app.Services.CreateAsyncScope();

    var runtimeSettingsStore =
        scope.ServiceProvider.GetRequiredService<
            IAiRuntimeSettingsStore>();
    var runtimeSettings =
        await runtimeSettingsStore.GetAsync(
            CancellationToken.None);

    var catalog =
        app.Services.GetRequiredService<
            BuiltInAgentSettingsCatalog>();
    var updatedAt = TimeProvider.System.GetUtcNow();

    var defaults = catalog.All
        .Select(
            definition =>
            {
                string? model = null;
                if (runtimeSettings?.AgentModels.TryGetValue(
                        definition.Agent.Id.Value,
                        out var legacyModel) == true)
                {
                    model = legacyModel;
                }

                return new AgentSettingsSnapshot(
                    definition.Agent.Id,
                    definition.Agent.Slug,
                    definition.Agent.DisplayName,
                    definition.Avatar,
                    definition.BubbleColor,
                    model,
                    definition.Prompt.SystemPrompt,
                    definition.RoutingDescription,
                    IsBuiltIn: true,
                    IsEnabled: true,
                    UpdatedAt: updatedAt);
            })
        .ToArray();

    var initializer =
        scope.ServiceProvider.GetRequiredService<
            InitializeAgentSettingsHandler>();
    await initializer.HandleAsync(
        defaults,
        CancellationToken.None);

}

static void TryDeleteLegacySettings(string? legacyPath)
{
    if (legacyPath is null || !File.Exists(legacyPath))
    {
        return;
    }

    try
    {
        File.Delete(legacyPath);
    }
    catch (IOException)
    {
        // The database is already the source of truth. A stale ignored
        // migration file must not prevent application startup.
    }
    catch (UnauthorizedAccessException)
    {
        // See comment above.
    }
}
