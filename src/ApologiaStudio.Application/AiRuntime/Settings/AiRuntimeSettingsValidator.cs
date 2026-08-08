using System.Text.RegularExpressions;

namespace ApologiaStudio.Application.AiRuntime.Settings;

public static partial class AiRuntimeSettingsValidator
{
    public static AiRuntimeSettingsSnapshot Normalize(
        UpdateAiRuntimeSettingsCommand command,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.AgentModels);

        var baseAddress =
            NormalizeBaseAddress(
                command.BaseAddress);

        var routingModel =
            NormalizeModel(
                command.RoutingModel,
                "The Ollama routing model is required.");

        var defaultAgentModel =
            NormalizeModel(
                command.DefaultAgentModel,
                "The default Ollama agent model is required.");

        ValidateRange(
            command.RoutingTimeoutSeconds,
            1,
            300,
            nameof(command.RoutingTimeoutSeconds));

        ValidateRange(
            command.GenerationTimeoutSeconds,
            1,
            600,
            nameof(command.GenerationTimeoutSeconds));

        ValidateRange(
            command.MaximumHistoryMessages,
            1,
            100,
            nameof(command.MaximumHistoryMessages));

        ValidateRange(
            command.MaximumHistoryCharacters,
            1_000,
            100_000,
            nameof(command.MaximumHistoryCharacters));

        ValidateRange(
            command.MaximumOutputTokens,
            64,
            8_192,
            nameof(command.MaximumOutputTokens));

        var keepAlive =
            command.KeepAlive?.Trim();

        if (string.IsNullOrWhiteSpace(keepAlive) ||
            !KeepAlivePattern().IsMatch(keepAlive))
        {
            throw new ArgumentException(
                "Keep-alive must be 0, -1, or a duration such as " +
                "30s, 10m, 1h, or 1h30m.",
                nameof(command));
        }

        var assignments =
            command.AgentModels
                .Where(
                    assignment =>
                        !string.IsNullOrWhiteSpace(
                            assignment.Model))
                .Select(
                    assignment =>
                    {
                        if (assignment.AgentId == Guid.Empty)
                        {
                            throw new ArgumentException(
                                "An agent model assignment cannot use an empty identifier.",
                                nameof(command));
                        }

                        return new KeyValuePair<Guid, string>(
                            assignment.AgentId,
                            NormalizeModel(
                                assignment.Model!,
                                "An assigned Ollama model is required."));
                    })
                .ToArray();

        if (assignments
            .GroupBy(assignment => assignment.Key)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "An agent can have at most one model assignment.",
                nameof(command));
        }

        return new AiRuntimeSettingsSnapshot(
            AiRuntimeSettingsSnapshot.OllamaProvider,
            baseAddress.ToString(),
            routingModel,
            defaultAgentModel,
            command.RoutingTimeoutSeconds,
            command.GenerationTimeoutSeconds,
            keepAlive,
            command.MaximumHistoryMessages,
            command.MaximumHistoryCharacters,
            command.MaximumOutputTokens,
            updatedAt,
            assignments.ToDictionary(
                assignment => assignment.Key,
                assignment => assignment.Value));
    }

    public static Uri NormalizeBaseAddress(
        string? baseAddressText)
    {
        if (!Uri.TryCreate(
                baseAddressText?.Trim(),
                UriKind.Absolute,
                out var baseAddress))
        {
            throw new ArgumentException(
                "The Ollama base address must be an absolute URI.",
                nameof(baseAddressText));
        }

        if (baseAddress.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "The Ollama base address must use HTTP or HTTPS.",
                nameof(baseAddressText));
        }

        if (!baseAddress.IsLoopback)
        {
            throw new ArgumentException(
                "The Ollama base address must target a loopback address. " +
                "The local Ollama API does not provide application-level " +
                "authentication.",
                nameof(baseAddressText));
        }

        return new Uri(
            baseAddress
                .ToString()
                .TrimEnd('/') + "/");
    }

    private static string NormalizeModel(
        string model,
        string requiredMessage)
    {
        var normalized = model?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(requiredMessage);
        }

        if (normalized.Length > 200)
        {
            throw new ArgumentException(
                "An Ollama model name cannot exceed 200 characters.");
        }

        return normalized;
    }

    private static void ValidateRange(
        int value,
        int minimum,
        int maximum,
        string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value must be between {minimum} and {maximum}.");
        }
    }

    [GeneratedRegex(
        @"^(?:0|-1|(?:\d+(?:\.\d+)?(?:ns|us|µs|ms|s|m|h))+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex KeepAlivePattern();
}
