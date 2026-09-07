using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Web;
using ApologiaStudio.Web.DocumentManager;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApologiaStudio.ArchitectureTests;

public sealed class CompositionRootTests
{
    [Fact]
    public void Critical_Services_Should_Have_Expected_Lifetimes()
    {
        var services = CreateServices();

        AssertLifetime<IAgentRoutingSnapshotProvider>(
            services,
            ServiceLifetime.Scoped);
        AssertLifetime<ISemanticRoutingClassifier>(
            services,
            ServiceLifetime.Scoped);
        AssertLifetime<IAgentRouter>(
            services,
            ServiceLifetime.Scoped);
        AssertLifetime<IAgentRuntime>(
            services,
            ServiceLifetime.Scoped);
        AssertLifetime<OllamaAgentRuntime>(
            services,
            ServiceLifetime.Scoped);
        AssertLifetime<SimulatedAgentRuntime>(
            services,
            ServiceLifetime.Scoped);
        AssertLifetime<IAgentRoutingTelemetry>(
            services,
            ServiceLifetime.Singleton);
        AssertLifetime<IOllamaRuntimeTelemetry>(
            services,
            ServiceLifetime.Singleton);
        AssertLifetime<TimeProvider>(
            services,
            ServiceLifetime.Singleton);
        AssertLifetime<DocumentManagerUiOptions>(
            services,
            ServiceLifetime.Singleton);
        AssertLifetime<DocumentManagerSessionBridgeOptions>(
            services,
            ServiceLifetime.Singleton);
        AssertLifetime<DocumentManagerSessionTicketIssuer>(
            services,
            ServiceLifetime.Singleton);
        AssertLifetime<IFieldSuggestionProvider>(
            services,
            ServiceLifetime.Scoped);
    }

    [Fact]
    public void Field_Suggestion_Capability_Should_Resolve_Without_Any_Model_Runtime()
    {
        // Machine assistance is optional. The container is built from the same
        // configuration as production, with no model, runtime or environment
        // variable of any kind, and the capability still resolves.
        var services = CreateServices();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        using var scope = provider.CreateScope();

        var suggestions = scope.ServiceProvider
            .GetRequiredService<IFieldSuggestionProvider>();

        Assert.IsType<UnavailableFieldSuggestionProvider>(suggestions);

        // Exactly one capability is registered: no collection, no keyed
        // service, no dynamic dispatch.
        Assert.Single(
            services.Where(x => x.ServiceType == typeof(IFieldSuggestionProvider)));
        Assert.Empty(
            scope.ServiceProvider
                .GetServices<IFieldSuggestionProvider>()
                .Skip(1));
    }

    [Fact]
    public void A_Configured_Encoder_Endpoint_Composes_The_Encoder_Capability()
    {
        // The only thing that turns machine assistance on is an endpoint. No
        // model path, no runtime detection, no container lifecycle.
        var services = CreateServices(
            new Dictionary<string, string?>
            {
                ["Encoder:BaseAddress"] = "http://127.0.0.1:5099/",
                ["Encoder:TimeoutSeconds"] = "45"
            });

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        using var scope = provider.CreateScope();

        Assert.IsType<EncoderBackedFieldSuggestionProvider>(
            scope.ServiceProvider.GetRequiredService<IFieldSuggestionProvider>());
        Assert.IsType<HttpEncoderInferenceRuntime>(
            scope.ServiceProvider.GetRequiredService<IEncoderInferenceRuntime>());

        // Still exactly one capability, and still resolved normally.
        Assert.Single(
            services.Where(x => x.ServiceType == typeof(IFieldSuggestionProvider)));

        var options = scope.ServiceProvider
            .GetRequiredService<EncoderInferenceOptions>();

        Assert.True(options.IsConfigured);
        Assert.Equal(TimeSpan.FromSeconds(45), options.Timeout);
    }

    [Fact]
    public void Composition_Root_Should_Build_With_Scope_Validation()
    {
        var services = CreateServices();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        Assert.IsType<DatabaseAgentRoutingSnapshotProvider>(
            scopedProvider.GetRequiredService<
                IAgentRoutingSnapshotProvider>());

        Assert.IsType<DynamicOllamaSemanticRoutingClassifier>(
            scopedProvider.GetRequiredService<
                ISemanticRoutingClassifier>());

        Assert.IsType<TelemetryAgentRouter>(
            scopedProvider.GetRequiredService<IAgentRouter>());

        Assert.IsType<BiblePassageAgentRuntime>(
            scopedProvider.GetRequiredService<IAgentRuntime>());

        Assert.IsType<SimulatedAgentRuntime>(
            scopedProvider.GetRequiredService<SimulatedAgentRuntime>());
    }

    private static ServiceCollection CreateServices(
        IReadOnlyDictionary<string, string?>? extraConfiguration = null)
    {
        var settings =
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ApologiaStudio"] =
                            "Host=127.0.0.1;Port=54329;" +
                            "Database=apologia_architecture_test;" +
                            "Username=apologia;Password=not-used",
                        ["ConnectionStrings:Knowledge"] =
                            "Host=127.0.0.1;Port=54330;" +
                            "Database=apologia_knowledge_architecture_test;" +
                            "Username=apologia;Password=not-used",
                        ["DocumentManager:UiUrl"] =
                            "http://localhost:5092/",
                        ["DocumentManager:SessionBridge:SharedSecret"] =
                            "architecture-tests-session-bridge-secret"
                    };

        foreach (var (key, value) in extraConfiguration ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddSingleton<EndpointDataSource>(
            new DefaultEndpointDataSource(Array.Empty<Endpoint>()));
        services.AddApologiaStudioWebServices(
            configuration,
            CreateRuntimeDefaults());

        return services;
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() =>
            Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(
            string uri,
            bool forceLoad)
        {
        }
    }

    private static AiRuntimeSettingsDefaults CreateRuntimeDefaults()
    {
        return new AiRuntimeSettingsDefaults(
            BaseAddress: "http://127.0.0.1:11434",
            RoutingModel: "qwen3:8b",
            DefaultAgentModel: "qwen3:8b",
            RoutingTimeoutSeconds: 60,
            GenerationTimeoutSeconds: 180,
            KeepAlive: "10m",
            MaximumHistoryMessages: 24,
            MaximumHistoryCharacters: 24_000,
            MaximumOutputTokens: 1_200);
    }

    private static void AssertLifetime<TService>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        var descriptors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(TService))
            .ToArray();

        var descriptor = Assert.Single(descriptors);

        Assert.Equal(
            expectedLifetime,
            descriptor.Lifetime);
    }
}
