namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public interface ISemanticRoutingClassifier
{
    ValueTask<SemanticRoutingResult> ClassifyAsync(
        string userMessage,
        CancellationToken cancellationToken);
}
