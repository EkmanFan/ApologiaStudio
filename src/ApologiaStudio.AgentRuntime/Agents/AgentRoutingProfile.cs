using ApologiaStudio.Application.Agents.Settings;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed record AgentRoutingProfile(
    AgentDescriptor Agent,
    string RoutingDescription)
{
    public static AgentRoutingProfile FromSettings(
        AgentSettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.Slug))
        {
            throw new ArgumentException(
                "An enabled agent must define a slug.",
                nameof(settings));
        }
        if (string.IsNullOrWhiteSpace(settings.RoutingDescription))
        {
            throw new ArgumentException(
                "An enabled agent must define a routing description.",
                nameof(settings));
        }

        return new AgentRoutingProfile(
            new AgentDescriptor(
                settings.AgentId,
                settings.Slug,
                settings.DisplayName),
            settings.RoutingDescription);
    }
}
