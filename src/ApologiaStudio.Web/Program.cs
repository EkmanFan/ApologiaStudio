using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Abstractions.Agents;
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
using ApologiaStudio.Web.Components;
using ApologiaStudio.Web.Endpoints;
using ApologiaStudio.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(
    builder.Configuration);

builder.Services.AddScoped<
    ICurrentUser,
    DemoCurrentUser>();

builder.Services.AddSingleton<TimeProvider>(
    TimeProvider.System);

builder.Services.AddSingleton<
    BiblePassageRequestParser>();

builder.Services.AddSingleton<
    DeterministicAgentRouter>();

// APOLOGIA-DYNAMIC-OLLAMA-ROUTING-SETTINGS
// appsettings provides startup defaults. The Settings page persists
// application-wide overrides read for every semantic-routing call.
var ollamaBaseUrlText =
    builder.Configuration["Ollama:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Ollama:BaseUrl is required.");

if (!Uri.TryCreate(
        ollamaBaseUrlText,
        UriKind.Absolute,
        out var ollamaBaseUri))
{
    throw new InvalidOperationException(
        "Ollama:BaseUrl must be an absolute URI.");
}

if (!ollamaBaseUri.IsLoopback)
{
    throw new InvalidOperationException(
        "Ollama must use a loopback address because its local API " +
        "does not provide application authentication.");
}

var ollamaModel =
    builder.Configuration["Ollama:RoutingModel"]
    ?? throw new InvalidOperationException(
        "Ollama:RoutingModel is required.");

var timeoutSeconds =
    builder.Configuration.GetValue<int?>(
        "Ollama:TimeoutSeconds")
    ?? throw new InvalidOperationException(
        "Ollama:TimeoutSeconds is required.");

var ollamaKeepAlive =
    builder.Configuration["Ollama:KeepAlive"]
    ?? throw new InvalidOperationException(
        "Ollama:KeepAlive is required.");

var routingSettingsRelativePath =
    builder.Configuration["Ollama:RoutingSettingsPath"]
    ?? throw new InvalidOperationException(
        "Ollama:RoutingSettingsPath is required.");

var normalizedBaseAddress =
    new Uri(
        ollamaBaseUri
            .ToString()
            .TrimEnd('/') + "/");

var initialOllamaRoutingSettings =
    new OllamaRoutingSettings(
        normalizedBaseAddress.ToString(),
        ollamaModel,
        timeoutSeconds,
        ollamaKeepAlive);

OllamaRoutingSettingsValidator.ToOptions(
    initialOllamaRoutingSettings);

var routingSettingsPath =
    Path.IsPathRooted(routingSettingsRelativePath)
        ? routingSettingsRelativePath
        : Path.Combine(
            builder.Environment.ContentRootPath,
            routingSettingsRelativePath);

builder.Services.AddSingleton<
    IOllamaRoutingSettingsStore>(
    new FileOllamaRoutingSettingsStore(
        routingSettingsPath,
        initialOllamaRoutingSettings));

builder.Services.AddSingleton<
    ISemanticRoutingClassifier>(
    serviceProvider =>
        new DynamicOllamaSemanticRoutingClassifier(
            serviceProvider.GetRequiredService<
                IOllamaRoutingSettingsStore>(),
            options =>
                new HttpClient
                {
                    BaseAddress = options.BaseAddress,
                    Timeout = options.RequestTimeout
                }));

builder.Services.AddSingleton(
    new HybridRoutingOptions());

builder.Services.AddSingleton<
    IAgentRouter,
    HybridAgentRouter>();

builder.Services.AddSingleton<
    SimulatedAgentResponseProvider>();

builder.Services.AddSingleton<
    SimulatedAgentRuntime>();

var ollamaResponseModel =
    builder.Configuration["Ollama:ResponseModel"]
    ?? ollamaModel;

var generationTimeoutSeconds =
    builder.Configuration.GetValue<int?>(
        "Ollama:GenerationTimeoutSeconds")
    ?? 180;

var maximumHistoryMessages =
    builder.Configuration.GetValue<int?>(
        "Ollama:MaximumHistoryMessages")
    ?? 24;

var maximumHistoryCharacters =
    builder.Configuration.GetValue<int?>(
        "Ollama:MaximumHistoryCharacters")
    ?? 24_000;

var maximumOutputTokens =
    builder.Configuration.GetValue<int?>(
        "Ollama:MaximumOutputTokens")
    ?? 1_200;

if (generationTimeoutSeconds is < 1 or > 600)
{
    throw new InvalidOperationException(
        "Ollama:GenerationTimeoutSeconds must be between 1 and 600.");
}

if (maximumHistoryMessages is < 1 or > 100)
{
    throw new InvalidOperationException(
        "Ollama:MaximumHistoryMessages must be between 1 and 100.");
}

if (maximumHistoryCharacters is < 1_000 or > 100_000)
{
    throw new InvalidOperationException(
        "Ollama:MaximumHistoryCharacters must be between 1000 and 100000.");
}

if (maximumOutputTokens is < 64 or > 8_192)
{
    throw new InvalidOperationException(
        "Ollama:MaximumOutputTokens must be between 64 and 8192.");
}

var ollamaGenerationOptions =
    new OllamaGenerationOptions
    {
        BaseAddress =
            normalizedBaseAddress,
        Model =
            ollamaResponseModel,
        RequestTimeout =
            TimeSpan.FromSeconds(
                generationTimeoutSeconds),
        KeepAlive =
            ollamaKeepAlive,
        MaximumHistoryMessages =
            maximumHistoryMessages,
        MaximumHistoryCharacters =
            maximumHistoryCharacters,
        MaximumOutputTokens =
            maximumOutputTokens
    };

builder.Services.AddSingleton(
    ollamaGenerationOptions);

builder.Services.AddSingleton<
    AgentPromptCatalog>();

builder.Services.AddSingleton<
    OllamaAgentRuntime>(
    serviceProvider =>
    {
        var client =
            new HttpClient
            {
                BaseAddress =
                    ollamaGenerationOptions.BaseAddress,
                Timeout =
                    ollamaGenerationOptions.RequestTimeout
            };

        return new OllamaAgentRuntime(
            serviceProvider.GetRequiredService<
                IAgentRouter>(),
            serviceProvider.GetRequiredService<
                AgentPromptCatalog>(),
            client,
            ollamaGenerationOptions);
    });

builder.Services.AddScoped<
    IAgentRuntime>(
    serviceProvider =>
        new BiblePassageAgentRuntime(
            serviceProvider.GetRequiredService<
                IAgentRouter>(),
            serviceProvider.GetRequiredService<
                IBibleCorpusQueryRepository>(),
            serviceProvider.GetRequiredService<
                OllamaAgentRuntime>()));

builder.Services.AddScoped<
    CreateConversationHandler>();

builder.Services.AddScoped<
    GetBibleReaderHandler>();

builder.Services.AddScoped<
    PrepareBibleDiscussionDraftHandler>();

builder.Services.AddScoped<
    DeleteConversationHandler>();

builder.Services.AddScoped<
    GetConversationHandler>();

builder.Services.AddScoped<
    ListConversationsHandler>();

builder.Services.AddScoped<
    MoveConversationHandler>();

builder.Services.AddScoped<
    GetSidebarNavigationHandler>();

builder.Services.AddScoped<
    SetSidebarPinHandler>();

builder.Services.AddScoped<
    ReorderProjectsHandler>();

builder.Services.AddScoped<
    ReorderPinnedItemsHandler>();

builder.Services.AddScoped<
    RenameConversationHandler>();

builder.Services.AddScoped<
    RestoreConversationHandler>();

builder.Services.AddScoped<
    CreateProjectHandler>();

builder.Services.AddScoped<
    RenameProjectHandler>();

builder.Services.AddScoped<
    DeleteProjectHandler>();

builder.Services.AddScoped<
    SendMessageHandler>();

builder.Services.AddScoped<
    GetUserPreferencesHandler>();

builder.Services.AddScoped<
    UpdateUserPreferencesHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapBibleCorpusEndpoints();

app.Run();
