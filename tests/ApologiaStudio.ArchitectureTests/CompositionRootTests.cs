using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Web;
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

    private static ServiceCollection CreateServices()
    {
        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ApologiaStudio"] =
                            "Host=127.0.0.1;Port=54329;" +
                            "Database=apologia_architecture_test;" +
                            "Username=apologia;Password=not-used"
                    })
                .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApologiaStudioWebServices(
            configuration,
            CreateRuntimeDefaults());

        return services;
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
