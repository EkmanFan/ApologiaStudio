using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed record AgentDescriptor(
    AgentId Id,
    string Slug,
    string DisplayName);
