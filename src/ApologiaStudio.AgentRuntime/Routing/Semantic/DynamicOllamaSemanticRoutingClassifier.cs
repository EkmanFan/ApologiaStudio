using System.Net.Http;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed class DynamicOllamaSemanticRoutingClassifier
    : ISemanticRoutingClassifier
{
    private readonly IOllamaRoutingSettingsStore _settingsStore;
    private readonly Func<OllamaRoutingOptions, HttpClient> _clientFactory;

    public DynamicOllamaSemanticRoutingClassifier(
        IOllamaRoutingSettingsStore settingsStore,
        Func<OllamaRoutingOptions, HttpClient> clientFactory)
    {
        _settingsStore =
            settingsStore
            ?? throw new ArgumentNullException(nameof(settingsStore));

        _clientFactory =
            clientFactory
            ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public ValueTask<SemanticRoutingResult> ClassifyAsync(
        string userMessage,
        CancellationToken cancellationToken)
    {
        var options =
            OllamaRoutingSettingsValidator.ToOptions(
                _settingsStore.Current);

        var client = _clientFactory(options);

        var classifier =
            new OllamaSemanticRoutingClassifier(
                client,
                options);

        return InvokeAndDisposeAsync(
            classifier,
            client,
            userMessage, cancellationToken);
    }

    private static async ValueTask<SemanticRoutingResult> InvokeAndDisposeAsync(
        OllamaSemanticRoutingClassifier classifier,
        HttpClient client,
        string userMessage,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            return await classifier
                .ClassifyAsync(userMessage, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
