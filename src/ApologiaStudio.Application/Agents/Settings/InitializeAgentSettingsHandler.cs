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

        var existing = (await settingsStore.ListAsync(cancellationToken))
            .ToDictionary(settings => settings.AgentId);
        var created = 0;

        foreach (var defaultSettings in defaults)
        {
            if (!existing.TryGetValue(
                    defaultSettings.AgentId,
                    out var current))
            {
                await settingsStore.SaveAsync(
                    defaultSettings,
                    cancellationToken);
                created++;
                continue;
            }

            if (!defaultSettings.IsBuiltIn)
            {
                continue;
            }

            var repaired = current with
            {
                Slug = defaultSettings.Slug,
                RoutingDescription = string.IsNullOrWhiteSpace(
                    current.RoutingDescription)
                    ? defaultSettings.RoutingDescription
                    : current.RoutingDescription,
                IsBuiltIn = true,
                IsEnabled = true
            };

            if (repaired == current)
            {
                continue;
            }

            await settingsStore.SaveAsync(
                repaired,
                cancellationToken);
        }

        return created;
    }
}
