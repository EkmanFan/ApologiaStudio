using ApologiaStudio.Application.Abstractions.AiRuntime;

namespace ApologiaStudio.Application.AiRuntime.Settings;

public sealed class UpdateAiRuntimeSettingsHandler(
    IAiRuntimeSettingsStore settingsStore,
    TimeProvider timeProvider)
{
    public async Task<AiRuntimeSettingsSnapshot> HandleAsync(
        UpdateAiRuntimeSettingsCommand command,
        CancellationToken cancellationToken)
    {
        var normalized =
            AiRuntimeSettingsValidator.Normalize(
                command,
                timeProvider.GetUtcNow());

        await settingsStore.SaveAsync(
            normalized,
            cancellationToken);

        return normalized;
    }
}
