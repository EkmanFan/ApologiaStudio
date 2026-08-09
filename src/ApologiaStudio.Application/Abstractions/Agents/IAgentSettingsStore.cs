using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.Abstractions.Agents;

public interface IAgentSettingsStore
{
    Task<IReadOnlyList<AgentSettingsSnapshot>> ListAsync(
        CancellationToken cancellationToken);

    Task<AgentSettingsSnapshot?> GetAsync(
        AgentId agentId,
        CancellationToken cancellationToken);

    Task<bool> TryCreateAsync(
        AgentSettingsSnapshot settings,
        int maximumActiveAgents,
        CancellationToken cancellationToken);

    Task SaveAsync(
        AgentSettingsSnapshot settings,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(
        AgentId agentId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}
