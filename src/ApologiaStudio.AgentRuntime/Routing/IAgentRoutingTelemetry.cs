namespace ApologiaStudio.AgentRuntime.Routing;

public interface IAgentRoutingTelemetry
{
    void RoutingCompleted(
        AgentRoutingCompletedObservation observation);
}
