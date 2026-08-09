using ApologiaStudio.Application.Abstractions.Agents;

namespace ApologiaStudio.Application.Agents.Settings;

public sealed class UpdateAgentSettingsHandler(
    IAgentSettingsStore settingsStore,
    TimeProvider timeProvider)
{
    public async Task<AgentSettingsSnapshot> HandleAsync(
        UpdateAgentSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await settingsStore.GetAsync(
            command.AgentId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"L'agent '{command.AgentId}' n'existe pas.");

        if (!existing.IsEnabled)
        {
            throw new InvalidOperationException(
                "Un agent supprimé ne peut pas être modifié.");
        }

        var normalized = AgentSettingsValidator.NormalizeUpdate(
            command,
            existing,
            timeProvider.GetUtcNow());
        await settingsStore.SaveAsync(
            normalized,
            cancellationToken);

        return normalized;
    }
}
