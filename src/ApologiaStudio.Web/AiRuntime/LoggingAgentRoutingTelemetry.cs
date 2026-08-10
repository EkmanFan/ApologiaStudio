using ApologiaStudio.AgentRuntime.Routing;

namespace ApologiaStudio.Web.AiRuntime;

public sealed class LoggingAgentRoutingTelemetry(
    ILogger<LoggingAgentRoutingTelemetry> logger)
    : IAgentRoutingTelemetry
{
    public void RoutingCompleted(
        AgentRoutingCompletedObservation observation)
    {
        logger.LogInformation(
            "Agent routing completed. " +
            "ConversationId={ConversationId} " +
            "RequestedAgentId={RequestedAgentId} " +
            "SelectedAgentId={SelectedAgentId} " +
            "WasExplicitlyRequested={WasExplicitlyRequested} " +
            "RoutingDurationMs={RoutingDurationMs:F1}",
            observation.ConversationId.Value,
            observation.RequestedAgentId?.Value,
            observation.SelectedAgentId.Value,
            observation.WasExplicitlyRequested,
            observation.DurationMilliseconds);
    }
}
