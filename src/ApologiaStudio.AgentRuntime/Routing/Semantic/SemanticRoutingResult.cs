namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed record SemanticRoutingResult(
    string AgentSlug,
    double Confidence,
    string Reason);
