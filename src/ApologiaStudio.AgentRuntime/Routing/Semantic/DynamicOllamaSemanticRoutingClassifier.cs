using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.Application.Abstractions.AiRuntime;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed class DynamicOllamaSemanticRoutingClassifier(
    IAiRuntimeSettingsStore settingsStore,
    IOllamaHttpClientFactory httpClientFactory,
    IAgentRegistry agentRegistry)
    : ISemanticRoutingClassifier
{
    public async ValueTask<SemanticRoutingResult> ClassifyAsync(
        string userMessage,
        CancellationToken cancellationToken)
    {
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
                agentRegistry.All);

        return await classifier
            .ClassifyAsync(
                userMessage,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
