using System.Text.RegularExpressions;

namespace ApologiaStudio.Application.Agents.Settings;

public static partial class AgentSettingsValidator
{
    public const int MaximumDisplayNameLength = 100;
    public const int MaximumAvatarLength = 32;
    public const int MaximumModelLength = 200;
    public const int MaximumSystemPromptLength = 30_000;

    public static AgentSettingsSnapshot Normalize(
        UpdateAgentSettingsCommand command,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.AgentId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "An agent identifier is required.",
                nameof(command));
        }

        var displayName = NormalizeRequired(
            command.DisplayName,
            MaximumDisplayNameLength,
            "The agent display name is required.");

        var avatar = NormalizeRequired(
            command.Avatar,
            MaximumAvatarLength,
            "The agent avatar is required.");

        var bubbleColor = command.BubbleColor?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(bubbleColor) ||
            !HexColorPattern().IsMatch(bubbleColor))
        {
            throw new ArgumentException(
                "The chat bubble color must use the #RRGGBB format.",
                nameof(command));
        }

        var model = command.Model?.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            model = null;
        }
        else if (model.Length > MaximumModelLength)
        {
            throw new ArgumentException(
                $"An Ollama model name cannot exceed {MaximumModelLength} characters.",
                nameof(command));
        }

        var systemPrompt = NormalizeRequired(
            command.SystemPrompt,
            MaximumSystemPromptLength,
            "The agent system prompt is required.");

        return new AgentSettingsSnapshot(
            command.AgentId,
            displayName,
            avatar,
            bubbleColor,
            model,
            systemPrompt,
            updatedAt);
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

    [GeneratedRegex(
        "^#[0-9A-F]{6}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HexColorPattern();
}
