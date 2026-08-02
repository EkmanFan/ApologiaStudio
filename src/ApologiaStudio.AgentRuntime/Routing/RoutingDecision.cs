using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Routing;

public enum BiblePassageResolution
{
    None,
    Resolved,
    Unsupported
}

public sealed record RoutingDecision(
    AgentId AgentId,
    string AgentName,
    string Reason,
    double Confidence,
    bool WasExplicitlyRequested,
    BiblePassageResolution BiblePassageResolution =
        BiblePassageResolution.None,
    BiblePassageRequest? BiblePassage = null);
