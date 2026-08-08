using ApologiaStudio.Application.Abstractions.AiRuntime;

namespace ApologiaStudio.Application.AiRuntime.Settings;

public sealed class GetAiRuntimeSettingsHandler(
    IAiRuntimeSettingsStore settingsStore)
{
    public async Task<AiRuntimeSettingsSnapshot> HandleAsync(
        CancellationToken cancellationToken)
    {
        return await settingsStore.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "AI runtime settings have not been initialized.");
    }
}
