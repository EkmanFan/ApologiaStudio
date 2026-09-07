using Microsoft.Extensions.Configuration;

namespace ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

/// <summary>
/// Reads where the resident encoder worker listens.
/// </summary>
/// <remarks>
/// Absent configuration is the normal state: Apologia starts, reviews metadata
/// and saves authoritatively with no encoder at all. Only a valid endpoint
/// turns the capability on.
///
/// Model paths are never read here. They belong to the worker, so an artifact
/// can move without an application change.
/// </remarks>
public static class EncoderInferenceConfiguration
{
    public const string SectionName = "Encoder";

    public static EncoderInferenceOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var address = section["BaseAddress"];

        if (string.IsNullOrWhiteSpace(address) ||
            !Uri.TryCreate(address, UriKind.Absolute, out var baseAddress))
        {
            return EncoderInferenceOptions.Disabled;
        }

        var timeout = int.TryParse(section["TimeoutSeconds"], out var seconds) &&
                      seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : EncoderInferenceOptions.Disabled.Timeout;

        // A trailing slash makes relative resource resolution predictable.
        if (!baseAddress.AbsolutePath.EndsWith('/'))
        {
            baseAddress = new Uri(baseAddress.AbsoluteUri + "/");
        }

        return new EncoderInferenceOptions(baseAddress, timeout);
    }

    /// <summary>
    /// Reads how, and whether, Apologia may start the worker itself.
    /// </summary>
    /// <remarks>
    /// No machine path is defaulted in code. Auto-start without an artifact
    /// root, an image or the worker source is simply not configured, and the
    /// supervisor treats that as a capability that stays unavailable.
    ///
    /// The port is taken from the endpoint rather than configured twice: two
    /// sources of truth for a port is a way to spend an afternoon.
    /// </remarks>
    public static EncoderWorkerOptions WorkerFromConfiguration(
        IConfiguration configuration,
        EncoderInferenceOptions inference,
        string defaultWorkerScriptPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(inference);

        if (!inference.IsConfigured)
        {
            return EncoderWorkerOptions.Disabled;
        }

        var section = configuration
            .GetSection(SectionName)
            .GetSection("Worker");

        var autoStart = !bool.TryParse(section["AutoStart"], out var enabled) || enabled;

        var scriptPath = section["ScriptPath"];

        return new EncoderWorkerOptions(
            autoStart,
            section["Image"] ?? string.Empty,
            section["ContainerName"] ?? "apologia-encoder-worker",
            section["ArtifactRoot"] ?? string.Empty,
            section["PrimaryModelPath"] ?? string.Empty,
            section["FallbackModelPath"] ?? string.Empty,
            string.IsNullOrWhiteSpace(scriptPath)
                ? defaultWorkerScriptPath
                : scriptPath,
            inference.BaseAddress!.Port,
            Positive(section["CpuThreads"], 8),
            Seconds(section["StartupTimeoutSeconds"], 60),
            !bool.TryParse(section["RestartEnabled"], out var restart) || restart,
            Seconds(section["MinimumRestartBackoffSeconds"], 5),
            Seconds(section["MaximumRestartBackoffSeconds"], 120));
    }

    private static int Positive(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static TimeSpan Seconds(string? value, int fallback) =>
        TimeSpan.FromSeconds(Positive(value, fallback));
}
