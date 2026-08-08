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
        var normalized = AgentSettingsValidator.Normalize(
            command,
            timeProvider.GetUtcNow());

        await settingsStore.SaveAsync(
            normalized,
            cancellationToken);

        return normalized;
    }
}
