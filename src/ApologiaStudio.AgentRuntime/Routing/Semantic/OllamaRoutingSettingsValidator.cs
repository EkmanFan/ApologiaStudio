using System.Text.RegularExpressions;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public static partial class OllamaRoutingSettingsValidator
{
    public static OllamaRoutingOptions ToOptions(
        OllamaRoutingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Uri.TryCreate(
                settings.BaseAddress?.Trim(),
                UriKind.Absolute,
                out var baseAddress))
        {
            throw new ArgumentException(
                "The Ollama base address must be an absolute URI.",
                nameof(settings));
        }

        if (baseAddress.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "The Ollama base address must use HTTP or HTTPS.",
                nameof(settings));
        }

        if (!baseAddress.IsLoopback)
        {
            throw new ArgumentException(
                "The Ollama base address must target a loopback address. " +
                "The local Ollama API does not provide application-level " +
                "authentication.",
                nameof(settings));
        }

        var model = settings.Model?.Trim();

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException(
                "The Ollama routing model is required.",
                nameof(settings));
        }

        if (model.Length > 200)
        {
            throw new ArgumentException(
                "The Ollama routing model cannot exceed 200 characters.",
                nameof(settings));
        }

        if (settings.RequestTimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "The routing timeout must be between 1 and 300 seconds.");
        }

        var keepAlive = settings.KeepAlive?.Trim();

        if (string.IsNullOrWhiteSpace(keepAlive) ||
            !KeepAlivePattern().IsMatch(keepAlive))
        {
            throw new ArgumentException(
                "Keep-alive must be 0, -1, or a duration such as 30s, " +
                "10m, 1h, or 1h30m.",
                nameof(settings));
        }

        return new OllamaRoutingOptions
        {
            BaseAddress =
                new Uri(
                    baseAddress
                        .ToString()
                        .TrimEnd('/') + "/"),
            Model = model,
            RequestTimeout =
                TimeSpan.FromSeconds(
                    settings.RequestTimeoutSeconds),
            KeepAlive = keepAlive
        };
    }

    [GeneratedRegex(
        @"^(?:0|-1|(?:\d+(?:\.\d+)?(?:ns|us|µs|ms|s|m|h))+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex KeepAlivePattern();
}
