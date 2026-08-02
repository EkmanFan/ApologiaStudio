using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Routing;

public sealed record RoutingDecision(
    AgentId AgentId,
    string AgentName,
    string Reason,
    double Confidence,
    bool WasExplicitlyRequested);
