using System.Diagnostics;
using ApologiaStudio.Application.Agents;

namespace ApologiaStudio.AgentRuntime.Routing;

public sealed class TelemetryAgentRouter(
    IAgentRouter inner,
    IAgentRoutingTelemetry telemetry)
    : IAgentRouter
{
    public async ValueTask<RoutingDecision> RouteAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = Stopwatch.GetTimestamp();

        var decision =
            await inner.RouteAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        telemetry.RoutingCompleted(
            new AgentRoutingCompletedObservation(
                request.ConversationId,
                request.RequestedAgentId,
                decision.AgentId,
                decision.WasExplicitlyRequested,
                Stopwatch.GetElapsedTime(
                    startedAt).TotalMilliseconds));

        return decision;
    }
}
