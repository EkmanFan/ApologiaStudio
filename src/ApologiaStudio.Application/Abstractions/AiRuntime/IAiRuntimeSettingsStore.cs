using ApologiaStudio.Application.AiRuntime.Settings;

namespace ApologiaStudio.Application.Abstractions.AiRuntime;

public interface IAiRuntimeSettingsStore
{
    Task<AiRuntimeSettingsSnapshot?> GetAsync(
        CancellationToken cancellationToken);

    Task SaveAsync(
        AiRuntimeSettingsSnapshot settings,
        CancellationToken cancellationToken);
}
