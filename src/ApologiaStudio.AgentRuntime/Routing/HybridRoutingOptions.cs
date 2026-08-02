namespace ApologiaStudio.AgentRuntime.Routing;

public sealed class HybridRoutingOptions
{
    public double DeterministicConfidenceThreshold { get; init; } =
        0.70;

    public double MinimumSemanticConfidence { get; init; } =
        0.65;
}
