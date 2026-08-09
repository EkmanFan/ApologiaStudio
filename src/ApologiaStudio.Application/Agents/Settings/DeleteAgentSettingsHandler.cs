using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.Agents.Settings;

public sealed class DeleteAgentSettingsHandler(
    IAgentSettingsStore settingsStore,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(
        AgentId agentId,
        CancellationToken cancellationToken)
    {
        var existing = await settingsStore.GetAsync(
            agentId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"L'agent '{agentId}' n'existe pas.");

        if (existing.IsBuiltIn)
        {
            throw new InvalidOperationException(
                "Les agents intégrés à ApologiaStudio ne peuvent pas être supprimés.");
        }

        if (!existing.IsEnabled)
        {
            return;
        }

        var deactivated = await settingsStore.DeactivateAsync(
            agentId,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (!deactivated)
        {
            throw new InvalidOperationException(
                "L'agent n'a pas pu être supprimé.");
        }
    }
}
