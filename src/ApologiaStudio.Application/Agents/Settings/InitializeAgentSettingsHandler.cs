using ApologiaStudio.Application.Abstractions.Agents;

namespace ApologiaStudio.Application.Agents.Settings;

public sealed class InitializeAgentSettingsHandler(
    IAgentSettingsStore settingsStore)
{
    public async Task<int> HandleAsync(
        IReadOnlyCollection<AgentSettingsSnapshot> defaults,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        var existing = await settingsStore.ListAsync(cancellationToken);
        var existingIds = existing
            .Select(settings => settings.AgentId)
            .ToHashSet();

        var created = 0;
        foreach (var defaultSettings in defaults)
        {
            if (existingIds.Contains(defaultSettings.AgentId))
            {
                continue;
            }

            await settingsStore.SaveAsync(
                defaultSettings,
                cancellationToken);
            created++;
        }

        return created;
    }
}
