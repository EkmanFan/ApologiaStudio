using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.AgentRuntime.Routing;

public sealed class HybridAgentRouter(
    DeterministicAgentRouter deterministicRouter,
    ISemanticRoutingClassifier semanticClassifier,
    HybridRoutingOptions options)
    : IAgentRouter
{
    public async ValueTask<RoutingDecision> RouteAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var deterministicDecision =
            deterministicRouter.Route(request);

        if (deterministicDecision.WasExplicitlyRequested)
        {
            return deterministicDecision;
        }

        if (deterministicDecision.Confidence >=
            options.DeterministicConfidenceThreshold)
        {
            return deterministicDecision;
        }

        var currentMessage =
            FindCurrentUserMessage(request);

        try
        {
            var semanticDecision =
                await semanticClassifier.ClassifyAsync(
                    currentMessage,
                    cancellationToken);

            if (semanticDecision.Confidence <
                options.MinimumSemanticConfidence)
            {
                return deterministicDecision with
                {
                    Reason =
                        deterministicDecision.Reason +
                        " Semantic classification confidence " +
                        $"was insufficient ({semanticDecision.Confidence:F2})."
                };
            }

            var selectedAgent =
                ResolveAgent(semanticDecision.AgentSlug);

            if (selectedAgent is null)
            {
                return deterministicDecision with
                {
                    Reason =
                        deterministicDecision.Reason +
                        " Semantic classification returned " +
                        "an unknown agent."
                };
            }

            return new RoutingDecision(
                selectedAgent.Id,
                selectedAgent.DisplayName,
                semanticDecision.Reason,
                semanticDecision.Confidence,
                WasExplicitlyRequested: false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return deterministicDecision with
            {
                Reason =
                    deterministicDecision.Reason +
                    " Semantic routing was unavailable; " +
                    "the deterministic fallback was used."
            };
        }
    }

    private static string FindCurrentUserMessage(
        AgentTurnRequest request)
    {
        var currentMessage =
            request.History.FirstOrDefault(
                message =>
                    message.MessageId ==
                        request.UserMessageId &&
                    message.Role ==
                        MessageRole.User);

        if (currentMessage is not null)
        {
            return currentMessage.Content;
        }

        var fallbackMessage =
            request.History.LastOrDefault(
                message =>
                    message.Role ==
                        MessageRole.User);

        if (fallbackMessage is null)
        {
            throw new InvalidOperationException(
                "The agent turn does not contain a user message.");
        }

        return fallbackMessage.Content;
    }

    private static AgentDescriptor? ResolveAgent(
        string agentSlug)
    {
        return BuiltInAgents.All.FirstOrDefault(
            agent =>
                string.Equals(
                    agent.Slug,
                    agentSlug,
                    StringComparison.OrdinalIgnoreCase));
    }
}
