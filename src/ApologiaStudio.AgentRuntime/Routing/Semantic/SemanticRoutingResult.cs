using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.BibleCorpora.Queries;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed record SemanticRoutingResult(
    string AgentSlug,
    double Confidence,
    string Reason,
    BiblePassageResolution BiblePassageResolution =
        BiblePassageResolution.None,
    BiblePassageRequest? BiblePassage = null);
