using System.Globalization;
using System.Text;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.AgentRuntime.Routing;

public sealed class DeterministicAgentRouter : IAgentRouter
{
    private static readonly string[] HistorianKeywords =
    [
        "a quelle epoque",
        "apparition",
        "apparaitre",
        "apparu",
        "chronologie",
        "date",
        "developpement historique",
        "histoire",
        "historique",
        "origine historique",
        "premieres preuves",
        "premier siecle",
        "siecle",
        "quand",
        "when",
        "history",
        "historical",
        "emerge",
        "emerged",
        "first evidence",
        "century",
        "timeline"
    ];

    private static readonly string[] ApologistKeywords =
    [
        "apologetique",
        "argument",
        "atheisme",
        "catholicisme",
        "comment defendre",
        "comment expliquer",
        "defendre",
        "defense de la foi",
        "foi chretienne",
        "islam",
        "objection",
        "orthodoxie",
        "repondre a",
        "resurrection",
        "trinite",
        "apologetics",
        "atheism",
        "catholicism",
        "defend",
        "explain",
        "faith",
        "islam",
        "objection",
        "orthodoxy",
        "resurrection",
        "trinity"
    ];

    public RoutingDecision Route(AgentTurnRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestedAgentId is { } requestedAgentId)
        {
            if (!BuiltInAgents.TryGet(
                    requestedAgentId,
                    out var requestedAgent))
            {
                throw new ArgumentException(
                    $"Requested agent '{requestedAgentId}' is not registered.",
                    nameof(request));
            }

            return new RoutingDecision(
                requestedAgent.Id,
                requestedAgent.DisplayName,
                "The user explicitly selected this agent.",
                1.0,
                WasExplicitlyRequested: true);
        }

        var currentMessage = FindCurrentUserMessage(request);
        var normalizedMessage = Normalize(currentMessage);

        var historianScore = CountMatches(
            normalizedMessage,
            HistorianKeywords);

        var apologistScore = CountMatches(
            normalizedMessage,
            ApologistKeywords);

        if (historianScore > apologistScore)
        {
            return CreateAutomaticDecision(
                BuiltInAgents.Historian,
                "The request primarily concerns historical development, chronology or historical evidence.",
                historianScore,
                apologistScore);
        }

        if (apologistScore > historianScore)
        {
            return CreateAutomaticDecision(
                BuiltInAgents.ProtestantApologist,
                "The request primarily concerns Christian apologetics, doctrine or the defence of faith.",
                apologistScore,
                historianScore);
        }

        return new RoutingDecision(
            BuiltInAgents.ProtestantApologist.Id,
            BuiltInAgents.ProtestantApologist.DisplayName,
            "No specialist signal was strong enough. The general Protestant apologist is used as the default agent.",
            0.55,
            WasExplicitlyRequested: false);
    }

    private static string FindCurrentUserMessage(
        AgentTurnRequest request)
    {
        var currentMessage = request.History.FirstOrDefault(
            message =>
                message.MessageId == request.UserMessageId &&
                message.Role == MessageRole.User);

        if (currentMessage is not null)
        {
            return currentMessage.Content;
        }

        var fallbackMessage = request.History.LastOrDefault(
            message => message.Role == MessageRole.User);

        if (fallbackMessage is null)
        {
            throw new InvalidOperationException(
                "The agent turn does not contain a user message.");
        }

        return fallbackMessage.Content;
    }

    private static RoutingDecision CreateAutomaticDecision(
        AgentDescriptor selectedAgent,
        string reason,
        int selectedScore,
        int otherScore)
    {
        var difference = selectedScore - otherScore;

        var confidence = difference switch
        {
            >= 3 => 0.90,
            2 => 0.82,
            _ => 0.70
        };

        return new RoutingDecision(
            selectedAgent.Id,
            selectedAgent.DisplayName,
            reason,
            confidence,
            WasExplicitlyRequested: false);
    }

    private static int CountMatches(
        string normalizedMessage,
        IEnumerable<string> keywords)
    {
        return keywords.Count(
            keyword => normalizedMessage.Contains(
                keyword,
                StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(
            NormalizationForm.FormD);

        var builder = new StringBuilder(
            decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(
                character);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(
                char.ToLowerInvariant(character));
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }
}
