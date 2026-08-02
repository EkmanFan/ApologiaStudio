using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Conversations.CreateConversation;
using ApologiaStudio.Application.Conversations.SendMessage;
using ApologiaStudio.Infrastructure.InMemory;
using ApologiaStudio.Web.Components;
using ApologiaStudio.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<
    IConversationRepository,
    InMemoryConversationRepository>();

builder.Services.AddScoped<
    IUnitOfWork,
    InMemoryUnitOfWork>();

builder.Services.AddScoped<
    ICurrentUser,
    DemoCurrentUser>();

builder.Services.AddSingleton<TimeProvider>(
    TimeProvider.System);

builder.Services.AddSingleton<
    IAgentRouter,
    DeterministicAgentRouter>();

builder.Services.AddSingleton<
    SimulatedAgentResponseProvider>();

builder.Services.AddSingleton<
    IAgentRuntime,
    SimulatedAgentRuntime>();

builder.Services.AddScoped<
    CreateConversationHandler>();

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
