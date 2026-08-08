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

    Task SaveAsync(
        AgentSettingsSnapshot settings,
        CancellationToken cancellationToken);
}
