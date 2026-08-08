using ApologiaStudio.Application.AiRuntime.Settings;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public static class OllamaRoutingSettingsValidator
{
    public static Uri NormalizeBaseAddress(
        string? baseAddressText)
    {
        return AiRuntimeSettingsValidator.NormalizeBaseAddress(
            baseAddressText);
    }

    public static OllamaRoutingOptions ToOptions(
        AiRuntimeSettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new OllamaRoutingOptions
        {
            BaseAddress =
                AiRuntimeSettingsValidator.NormalizeBaseAddress(
                    settings.BaseAddress),
            Model = settings.RoutingModel,
            RequestTimeout =
                TimeSpan.FromSeconds(
                    settings.RoutingTimeoutSeconds),
            KeepAlive = settings.KeepAlive
        };
    }
}
