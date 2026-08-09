using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.AgentRuntime.Routing;

public sealed class HybridAgentRouter(
    DeterministicAgentRouter deterministicRouter,
    ISemanticRoutingClassifier semanticClassifier,
    HybridRoutingOptions options,
    IAgentRegistry? agentRegistry = null)
    : IAgentRouter
{
    private readonly IAgentRegistry _agentRegistry =
        agentRegistry ?? new BuiltInAgentRegistry();

    public async ValueTask<RoutingDecision> RouteAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var deterministicDecision =
            deterministicRouter.Route(request);

        var needsBibleIntentClassification =
            deterministicDecision.WasExplicitlyRequested &&
            deterministicDecision.AgentId ==
                BuiltInAgents.ProtestantApologist.Id &&
            deterministicDecision.BiblePassageResolution ==
                BiblePassageResolution.None;

        if (deterministicDecision.WasExplicitlyRequested &&
            !needsBibleIntentClassification)
        {
            return deterministicDecision;
        }

        if (!needsBibleIntentClassification &&
            deterministicDecision.BiblePassageResolution !=
                BiblePassageResolution.None)
        {
            return deterministicDecision;
        }

        var hasCustomAgents =
            _agentRegistry.All.Count > BuiltInAgents.All.Count;

        if (!needsBibleIntentClassification &&
            !hasCustomAgents &&
            deterministicDecision.Confidence >=
            options.DeterministicConfidenceThreshold)
        {
            return deterministicDecision;
        }

        var currentMessage =
            FindCurrentUserMessage(request);

        var isBiblePassageLookupCandidate =
            deterministicRouter.IsBiblePassageLookupCandidate(
                currentMessage);

        try
        {
            var semanticDecision =
                await semanticClassifier.ClassifyAsync(
                    currentMessage,
                    cancellationToken);

            if (semanticDecision.BiblePassageResolution !=
                BiblePassageResolution.None)
            {
                var resolution =
                    semanticDecision.BiblePassageResolution ==
                        BiblePassageResolution.Resolved &&
                    semanticDecision.BiblePassage is not null
                        ? BiblePassageResolution.Resolved
                        : BiblePassageResolution.Unsupported;

                return new RoutingDecision(
                    BuiltInAgents.ProtestantApologist.Id,
                    BuiltInAgents.ProtestantApologist.DisplayName,
                    semanticDecision.Reason,
                    semanticDecision.Confidence,
                    WasExplicitlyRequested:
                        deterministicDecision.WasExplicitlyRequested,
                    resolution,
                    resolution == BiblePassageResolution.Resolved
                        ? semanticDecision.BiblePassage
                        : null);
            }

            if (isBiblePassageLookupCandidate)
            {
                return new RoutingDecision(
                    BuiltInAgents.ProtestantApologist.Id,
                    BuiltInAgents.ProtestantApologist.DisplayName,
                    semanticDecision.Reason,
                    semanticDecision.Confidence,
                    WasExplicitlyRequested:
                        deterministicDecision.WasExplicitlyRequested,
                    BiblePassageResolution.Unsupported);
            }

            if (deterministicDecision.WasExplicitlyRequested)
            {
                return deterministicDecision;
            }

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
            if (isBiblePassageLookupCandidate)
            {
                return new RoutingDecision(
                    BuiltInAgents.ProtestantApologist.Id,
                    BuiltInAgents.ProtestantApologist.DisplayName,
                    deterministicDecision.Reason +
                    " Bible-reference normalization was unavailable.",
                    deterministicDecision.Confidence,
                    WasExplicitlyRequested:
                        deterministicDecision.WasExplicitlyRequested,
                    BiblePassageResolution.Unsupported);
            }

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

    private AgentDescriptor? ResolveAgent(
        string agentSlug)
    {
        return _agentRegistry.TryGet(
            agentSlug,
            out var profile)
                ? profile.Agent
                : null;
    }
}
