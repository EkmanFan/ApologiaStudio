using ApologiaStudio.AgentRuntime.Agents;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public interface ISemanticRoutingClassifier
{
    ValueTask<SemanticRoutingResult> ClassifyAsync(
        string userMessage,
        IReadOnlyList<AgentRoutingProfile> routingProfiles,
        CancellationToken cancellationToken);
}
