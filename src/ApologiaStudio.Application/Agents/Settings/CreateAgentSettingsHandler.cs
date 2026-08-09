using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.Agents.Settings;

public sealed class CreateAgentSettingsHandler(
    IAgentSettingsStore settingsStore,
    TimeProvider timeProvider)
{
    public async Task<AgentSettingsSnapshot> HandleAsync(
        CreateAgentSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var agentId = AgentId.New();
        var slug = $"custom-{agentId.Value:N}";
        var normalized = AgentSettingsValidator.NormalizeCreate(
            agentId,
            slug,
            command,
            timeProvider.GetUtcNow());

        var created = await settingsStore.TryCreateAsync(
            normalized,
            AgentSettingsPolicy.MaximumActiveAgents,
            cancellationToken);
        if (!created)
        {
            throw new InvalidOperationException(
                $"ApologiaStudio accepte au maximum {AgentSettingsPolicy.MaximumActiveAgents} agents actifs.");
        }

        return normalized;
    }
}
