using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.AgentRuntime.Routing;

public sealed record AgentRoutingCompletedObservation(
    ConversationId ConversationId,
    AgentId? RequestedAgentId,
    AgentId SelectedAgentId,
    bool WasExplicitlyRequested,
    double DurationMilliseconds);
