using ApologiaStudio.Application.Abstractions.AiRuntime;

namespace ApologiaStudio.Application.AiRuntime.Settings;

public sealed class InitializeAiRuntimeSettingsHandler(
    IAiRuntimeSettingsStore settingsStore)
{
    public async Task<bool> HandleAsync(
        AiRuntimeSettingsSnapshot initialSettings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(initialSettings);

        if (await settingsStore.GetAsync(cancellationToken)
            is not null)
        {
            return false;
        }

        await settingsStore.SaveAsync(
            initialSettings,
            cancellationToken);

        return true;
    }
}
