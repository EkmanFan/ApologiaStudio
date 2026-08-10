using System.Globalization;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;
using Microsoft.AspNetCore.Components;
using ConversationMessageModel = ApologiaStudio.Domain.Conversations.ConversationMessage;
using MessageRole = ApologiaStudio.Domain.Conversations.MessageRole;

namespace ApologiaStudio.Web.Components.Conversations;

public partial class ConversationMessage
{
    [Parameter, EditorRequired]
    public ConversationMessageModel Message { get; set; } = null!;

    [Parameter, EditorRequired]
    public IReadOnlyDictionary<AgentId, AgentSettingsSnapshot> AgentSettings { get; set; } =
        new Dictionary<AgentId, AgentSettingsSnapshot>();

    [Parameter]
    public bool IsEnglish { get; set; }

    [Parameter, EditorRequired]
    public string DateFormat { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string TimeFormat { get; set; } = string.Empty;

    private string MessageCssClass =>
        Message.Role switch
        {
            MessageRole.User => "message user",
            MessageRole.Agent => "message agent",
            _ => "message system"
        };

    private string? MessageStyle =>
        Message.Role == MessageRole.Agent
            ? GetAgentStyle(Message.AgentId)
            : null;

    private string MessageAuthor
    {
        get
        {
            if (Message.Role == MessageRole.User)
            {
                return IsEnglish ? "You" : "Vous";
            }

            if (Message.AgentId is { } agentId &&
                GetAgentSettings(agentId) is { } settings)
            {
                return settings.DisplayName;
            }

            return IsEnglish ? "System" : "Système";
        }
    }

    private string AgentAvatar =>
        Message.AgentId is { } agentId
            ? GetAgentSettings(agentId)?.Avatar ?? "AI"
            : "AI";

    private string MessageTimestamp
    {
        get
        {
            var localTimestamp = Message.CreatedAt.ToLocalTime();

            return localTimestamp.ToString(
                       DateFormat,
                       CultureInfo.InvariantCulture) +
                   " " +
                   localTimestamp.ToString(
                       TimeFormat,
                       CultureInfo.InvariantCulture);
        }
    }

    private string MessageTimestampMachineValue =>
        Message.CreatedAt
            .ToUniversalTime()
            .ToString(
                "O",
                CultureInfo.InvariantCulture);

    private string? GetAgentStyle(AgentId? agentId)
    {
        if (agentId is null ||
            GetAgentSettings(agentId.Value) is not { } settings)
        {
            return null;
        }

        var textColor = GetContrastTextColor(settings.BubbleColor);

        return $"--agent-bubble-color:{settings.BubbleColor};" +
               $"--agent-text-color:{textColor};";
    }

    private AgentSettingsSnapshot? GetAgentSettings(AgentId agentId)
    {
        return AgentSettings.TryGetValue(
            agentId,
            out var settings)
            ? settings
            : null;
    }

    private static string GetContrastTextColor(string color)
    {
        if (color.Length != 7 ||
            color[0] != '#' ||
            !int.TryParse(
                color.AsSpan(1, 2),
                NumberStyles.HexNumber,
                null,
                out var red) ||
            !int.TryParse(
                color.AsSpan(3, 2),
                NumberStyles.HexNumber,
                null,
                out var green) ||
            !int.TryParse(
                color.AsSpan(5, 2),
                NumberStyles.HexNumber,
                null,
                out var blue))
        {
            return "#252823";
        }

        var luminance =
            (0.299 * red + 0.587 * green + 0.114 * blue) / 255.0;

        return luminance > 0.58
            ? "#252823"
            : "#FFFFFF";
    }
}
