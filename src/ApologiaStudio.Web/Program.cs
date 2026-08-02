using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Conversations.CreateConversation;
using ApologiaStudio.Application.Conversations.GetConversation;
using ApologiaStudio.Application.Conversations.ListConversations;
using ApologiaStudio.Application.Conversations.RenameConversation;
using ApologiaStudio.Application.Conversations.SendMessage;
using ApologiaStudio.Infrastructure;
using ApologiaStudio.Web.Components;
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
    DeterministicAgentRouter>();

var ollamaBaseUrlText =
    builder.Configuration["Ollama:BaseUrl"]
    ?? "http://127.0.0.1:11434";

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
    ?? "qwen3:8b";

var timeoutSeconds =
    builder.Configuration.GetValue<int?>(
        "Ollama:TimeoutSeconds")
    ?? 60;

if (timeoutSeconds is < 1 or > 300)
{
    throw new InvalidOperationException(
        "Ollama:TimeoutSeconds must be between 1 and 300.");
}

var normalizedBaseAddress =
    new Uri(
        ollamaBaseUri
            .ToString()
            .TrimEnd('/') + "/");

var ollamaOptions =
    new OllamaRoutingOptions
    {
        BaseAddress =
            normalizedBaseAddress,
        Model =
            ollamaModel,
        RequestTimeout =
            TimeSpan.FromSeconds(
                timeoutSeconds),
        KeepAlive =
            builder.Configuration["Ollama:KeepAlive"]
            ?? "10m"
    };

builder.Services.AddSingleton(
    ollamaOptions);

builder.Services.AddSingleton<
    ISemanticRoutingClassifier>(
    _ =>
    {
        var httpClient =
            new HttpClient
            {
                BaseAddress =
                    ollamaOptions.BaseAddress,
                Timeout =
                    ollamaOptions.RequestTimeout
            };

        return new OllamaSemanticRoutingClassifier(
            httpClient,
            ollamaOptions);
    });

builder.Services.AddSingleton(
    new HybridRoutingOptions());

builder.Services.AddSingleton<
    IAgentRouter,
    HybridAgentRouter>();

builder.Services.AddSingleton<
    SimulatedAgentResponseProvider>();

builder.Services.AddSingleton<
    IAgentRuntime,
    SimulatedAgentRuntime>();

builder.Services.AddScoped<
    CreateConversationHandler>();

builder.Services.AddScoped<
    GetConversationHandler>();

builder.Services.AddScoped<
    ListConversationsHandler>();

builder.Services.AddScoped<
    RenameConversationHandler>();

builder.Services.AddScoped<
    SendMessageHandler>();

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

app.Run();
