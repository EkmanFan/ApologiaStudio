using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.Agents.Settings;

public sealed class GetAgentSettingsHandler(
    IAgentSettingsStore settingsStore)
{
    public Task<IReadOnlyList<AgentSettingsSnapshot>> HandleAsync(
        CancellationToken cancellationToken)
    {
        return settingsStore.ListAsync(cancellationToken);
    }

    public Task<AgentSettingsSnapshot?> HandleAsync(
        AgentId agentId,
        CancellationToken cancellationToken)
    {
        return settingsStore.GetAsync(agentId, cancellationToken);
    }
}
