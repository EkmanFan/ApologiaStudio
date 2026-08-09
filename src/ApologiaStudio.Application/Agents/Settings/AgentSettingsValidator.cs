using System.Text.RegularExpressions;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Application.Agents.Settings;

public static partial class AgentSettingsValidator
{
    public const int MaximumDisplayNameLength = 100;
    public const int MaximumAvatarLength = 32;
    public const int MaximumModelLength = 200;
    public const int MaximumSystemPromptLength = 30_000;
    public const int MaximumRoutingDescriptionLength = 4_000;
    public const int MaximumSlugLength = 80;

    public static AgentSettingsSnapshot NormalizeUpdate(
        UpdateAgentSettingsCommand command,
        AgentSettingsSnapshot existing,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(existing);

        if (command.AgentId != existing.AgentId)
        {
            throw new ArgumentException(
                "The agent identifier does not match the persisted profile.",
                nameof(command));
        }

        var values = NormalizeValues(
            command.DisplayName,
            command.Avatar,
            command.BubbleColor,
            command.Model,
            command.SystemPrompt,
            command.RoutingDescription,
            nameof(command));

        return existing with
        {
            DisplayName = values.DisplayName,
            Avatar = values.Avatar,
            BubbleColor = values.BubbleColor,
            Model = values.Model,
            SystemPrompt = values.SystemPrompt,
            RoutingDescription = values.RoutingDescription,
            UpdatedAt = updatedAt
        };
    }

    public static AgentSettingsSnapshot NormalizeCreate(
        AgentId agentId,
        string slug,
        CreateAgentSettingsCommand command,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (agentId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "An agent identifier is required.",
                nameof(agentId));
        }

        var normalizedSlug = NormalizeRequired(
            slug,
            MaximumSlugLength,
            "The agent slug is required.");
        if (!SlugPattern().IsMatch(normalizedSlug))
        {
            throw new ArgumentException(
                "The agent slug may contain only lowercase letters, numbers and hyphens.",
                nameof(slug));
        }

        var values = NormalizeValues(
            command.DisplayName,
            command.Avatar,
            command.BubbleColor,
            command.Model,
            command.SystemPrompt,
            command.RoutingDescription,
            nameof(command));

        return new AgentSettingsSnapshot(
            agentId,
            normalizedSlug,
            values.DisplayName,
            values.Avatar,
            values.BubbleColor,
            values.Model,
            values.SystemPrompt,
            values.RoutingDescription,
            IsBuiltIn: false,
            IsEnabled: true,
            UpdatedAt: updatedAt);
    }

    private static NormalizedAgentValues NormalizeValues(
        string? displayName,
        string? avatar,
        string? bubbleColor,
        string? model,
        string? systemPrompt,
        string? routingDescription,
        string parameterName)
    {
        var normalizedDisplayName = NormalizeRequired(
            displayName,
            MaximumDisplayNameLength,
            "The agent display name is required.");
        var normalizedAvatar = NormalizeRequired(
            avatar,
            MaximumAvatarLength,
            "The agent avatar is required.");

        var normalizedBubbleColor = bubbleColor?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedBubbleColor) ||
            !HexColorPattern().IsMatch(normalizedBubbleColor))
        {
            throw new ArgumentException(
                "The chat bubble color must use the #RRGGBB format.",
                parameterName);
        }

        var normalizedModel = model?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            normalizedModel = null;
        }
        else if (normalizedModel.Length > MaximumModelLength)
        {
            throw new ArgumentException(
                $"An Ollama model name cannot exceed {MaximumModelLength} characters.",
                parameterName);
        }

        var normalizedSystemPrompt = NormalizeRequired(
            systemPrompt,
            MaximumSystemPromptLength,
            "The agent system prompt is required.");
        var normalizedRoutingDescription = NormalizeRequired(
            routingDescription,
            MaximumRoutingDescriptionLength,
            "The routing description is required.");

        return new NormalizedAgentValues(
            normalizedDisplayName,
            normalizedAvatar,
            normalizedBubbleColor,
            normalizedModel,
            normalizedSystemPrompt,
            normalizedRoutingDescription);
    }

    private static string NormalizeRequired(
        string? value,
        int maximumLength,
        string requiredMessage)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(requiredMessage);
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private sealed record NormalizedAgentValues(
        string DisplayName,
        string Avatar,
        string BubbleColor,
        string? Model,
        string SystemPrompt,
        string RoutingDescription);

    [GeneratedRegex(
        "^#[0-9A-F]{6}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HexColorPattern();

    [GeneratedRegex(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
