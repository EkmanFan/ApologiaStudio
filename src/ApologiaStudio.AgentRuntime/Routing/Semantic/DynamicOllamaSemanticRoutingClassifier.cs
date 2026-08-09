using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.Application.Abstractions.AiRuntime;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed class DynamicOllamaSemanticRoutingClassifier(
    IAiRuntimeSettingsStore settingsStore,
    IOllamaHttpClientFactory httpClientFactory)
    : ISemanticRoutingClassifier
{
    public async ValueTask<SemanticRoutingResult> ClassifyAsync(
        string userMessage,
        IReadOnlyList<AgentRoutingProfile> routingProfiles,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(routingProfiles);

        var settings =
            await settingsStore.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "AI runtime settings have not been initialized.");

        var options =
            OllamaRoutingSettingsValidator.ToOptions(settings);

        using var client =
            httpClientFactory.Create(
                options.BaseAddress,
                options.RequestTimeout);

        using var classifier =
            new OllamaSemanticRoutingClassifier(
                client,
                options,
                routingProfiles);

        return await classifier
            .ClassifyAsync(
                userMessage,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
