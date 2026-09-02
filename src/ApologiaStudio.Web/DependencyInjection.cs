using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Application.AiRuntime.Settings;
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
using ApologiaStudio.Application.Navigation.GetSidebarNavigation;
using ApologiaStudio.Application.Navigation.ReorderPinnedItems;
using ApologiaStudio.Application.Navigation.ReorderProjects;
using ApologiaStudio.Application.Navigation.SetSidebarPin;
using ApologiaStudio.Application.Preferences;
using ApologiaStudio.Application.Projects.CreateProject;
using ApologiaStudio.Application.Projects.DeleteProject;
using ApologiaStudio.Application.Projects.RenameProject;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Infrastructure;
using ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;
using ApologiaStudio.Web.AiRuntime;
using ApologiaStudio.Web.DocumentManager;
using ApologiaStudio.Web.Identity;

namespace ApologiaStudio.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddApologiaStudioWebServices(
        this IServiceCollection services,
        IConfiguration configuration,
        AiRuntimeSettingsDefaults aiRuntimeDefaults)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(aiRuntimeDefaults);

        services.AddInfrastructure(configuration);

        services.AddSingleton(
            DocumentManagerUiOptions.FromConfiguration(
                configuration));
        services.AddSingleton(
            DocumentManagerAdministrationOptions.FromConfiguration(
                configuration));
        var documentManagerConsumerOptions =
            DocumentManagerConsumerOptions.FromConfiguration(
                configuration);
        services.AddSingleton(documentManagerConsumerOptions);
        services.AddSingleton<DocumentManagerConsumptionSignal>();
        services.AddSingleton<
            IDocumentManagerAdministrationAuthorizer,
            ConfiguredDocumentManagerAdministrationAuthorizer>();

        if (documentManagerConsumerOptions.Enabled)
        {
            services.AddSingleton(
                documentManagerConsumerOptions.Manager!);
            services.AddHttpClient<
                IDocumentManagerResultSource,
                HttpDocumentManagerResultSource>(
                client =>
                {
                    client.BaseAddress =
                        documentManagerConsumerOptions.Manager!.BaseAddress;
                    client.Timeout =
                        documentManagerConsumerOptions.RequestTimeout;
                });
            if (documentManagerConsumerOptions.CanRequestReplay)
            {
                services.AddHttpClient<
                    IDocumentManagerDeliveryReplayClient,
                    HttpDocumentManagerDeliveryReplayClient>(
                    client =>
                    {
                        client.BaseAddress =
                            documentManagerConsumerOptions.Manager!.BaseAddress;
                        client.Timeout =
                            documentManagerConsumerOptions.RequestTimeout;
                    });
            }
            services.AddHostedService<
                DocumentManagerTriggeredConsumerHostedService>();
            services.AddScoped<
                ConsumeDocumentManagerResultHandler>();
        }

        services.AddScoped<
            ICurrentUser,
            DemoCurrentUser>();

        services.AddSingleton<
            BiblePassageRequestParser>();

        services.AddScoped<
            IAgentRoutingSnapshotProvider,
            DatabaseAgentRoutingSnapshotProvider>();

        services.AddSingleton<
            DeterministicAgentRouter>();

        services.AddSingleton(
            aiRuntimeDefaults);

        services.AddHttpClient<
            IOllamaModelCatalogClient,
            OllamaModelCatalogClient>(
            client =>
                client.Timeout =
                    TimeSpan.FromSeconds(10));

        services.AddHttpClient(
            "ApologiaStudio.Ollama.Dynamic");

        services.AddSingleton<
            IOllamaHttpClientFactory,
            OllamaHttpClientFactory>();

        services.AddSingleton(
            new HybridRoutingOptions());

        services.AddSingleton<
            SimulatedAgentResponseProvider>();

        services.AddScoped<
            SimulatedAgentRuntime>();

        services.AddScoped<
            ISemanticRoutingClassifier,
            DynamicOllamaSemanticRoutingClassifier>();

        services.AddScoped<
            HybridAgentRouter>();

        services.AddSingleton<
            IAgentRoutingTelemetry,
            LoggingAgentRoutingTelemetry>();

        services.AddScoped<
            IAgentRouter>(
            serviceProvider =>
                new TelemetryAgentRouter(
                    serviceProvider.GetRequiredService<
                        HybridAgentRouter>(),
                    serviceProvider.GetRequiredService<
                        IAgentRoutingTelemetry>()));

        services.AddSingleton<
            AgentPromptCatalog>();
        services.AddSingleton<
            BuiltInAgentSettingsCatalog>();

        services.AddSingleton<
            IOllamaRuntimeTelemetry,
            LoggingOllamaRuntimeTelemetry>();

        services.AddScoped<
            OllamaAgentRuntime>();

        services.AddScoped<
            IAgentRuntime>(
            serviceProvider =>
                new BiblePassageAgentRuntime(
                    serviceProvider.GetRequiredService<
                        IAgentRouter>(),
                    serviceProvider.GetRequiredService<
                        IBibleCorpusQueryRepository>(),
                    serviceProvider.GetRequiredService<
                        OllamaAgentRuntime>()));

        services.AddScoped<
            CreateConversationHandler>();
        services.AddScoped<
            GetBibleReaderHandler>();
        services.AddScoped<
            PrepareBibleDiscussionDraftHandler>();
        services.AddScoped<
            DeleteConversationHandler>();
        services.AddScoped<
            GetConversationHandler>();
        services.AddScoped<
            ListConversationsHandler>();
        services.AddScoped<
            MoveConversationHandler>();
        services.AddScoped<
            GetSidebarNavigationHandler>();
        services.AddScoped<
            SetSidebarPinHandler>();
        services.AddScoped<
            ReorderProjectsHandler>();
        services.AddScoped<
            ReorderPinnedItemsHandler>();
        services.AddScoped<
            RenameConversationHandler>();
        services.AddScoped<
            RestoreConversationHandler>();
        services.AddScoped<
            CreateProjectHandler>();
        services.AddScoped<
            RenameProjectHandler>();
        services.AddScoped<
            DeleteProjectHandler>();
        services.AddScoped<
            SendMessageHandler>();
        services.AddScoped<
            GetUserPreferencesHandler>();
        services.AddScoped<
            UpdateUserPreferencesHandler>();
        services.AddScoped<
            GetAiRuntimeSettingsHandler>();
        services.AddScoped<
            InitializeAiRuntimeSettingsHandler>();
        services.AddScoped<
            UpdateAiRuntimeSettingsHandler>();
        services.AddScoped<
            GetAgentSettingsHandler>();
        services.AddScoped<
            InitializeAgentSettingsHandler>();
        services.AddScoped<
            UpdateAgentSettingsHandler>();
        services.AddScoped<
            CreateAgentSettingsHandler>();
        services.AddScoped<
            DeleteAgentSettingsHandler>();
        services.AddScoped<
            ListDocumentManagerEditorialDraftsHandler>();
        services.AddScoped<
            GetDocumentManagerEditorialDraftHandler>();
        services.AddScoped<
            ReviewDocumentManagerEditorialDraftHandler>();
        services.AddScoped<
            ReopenDocumentManagerEditorialDraftHandler>();
        services.AddScoped<
            PurgeDocumentManagerSubmissionHandler>();

        return services;
    }
}
