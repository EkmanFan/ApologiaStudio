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
}
