using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.Agents;

public abstract record AgentRunEvent;

public sealed record AgentSelectedEvent(
    AgentId AgentId,
    string AgentName,
    string Reason) : AgentRunEvent;

public sealed record TextDeltaEvent(
    string Content) : AgentRunEvent;

public sealed record AgentTurnCompletedEvent(
    AgentId AgentId,
    string Content) : AgentRunEvent;
