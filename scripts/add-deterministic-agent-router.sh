#!/usr/bin/env bash

set -Eeuo pipefail

trap 'status=$?
echo
echo "ERROR at line ${LINENO}: ${BASH_COMMAND}"
echo "Exit code: ${status}"
exit "${status}"' ERR

cd "$(dirname "$0")/.."

if [[ ! -f "ApologiaStudio.sln" ]]; then
  echo "ERROR: ApologiaStudio.sln was not found."
  exit 1
fi

echo "Creating agent routing directories..."

mkdir -p \
  src/ApologiaStudio.AgentRuntime/Agents \
  src/ApologiaStudio.AgentRuntime/Routing \
  tests/ApologiaStudio.UnitTests/AgentRuntime/Routing

echo "Creating built-in agent descriptors..."

cat > src/ApologiaStudio.AgentRuntime/Agents/AgentDescriptor.cs <<'EOF'
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed record AgentDescriptor(
    AgentId Id,
    string Slug,
    string DisplayName);
EOF

cat > src/ApologiaStudio.AgentRuntime/Agents/BuiltInAgents.cs <<'EOF'
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public static class BuiltInAgents
{
    public static readonly AgentDescriptor Historian = new(
        new AgentId(
            Guid.Parse("11111111-1111-1111-1111-111111111111")),
        "historian",
        "Historian of Religions");

    public static readonly AgentDescriptor ProtestantApologist = new(
        new AgentId(
            Guid.Parse("22222222-2222-2222-2222-222222222222")),
        "protestant-apologist",
        "Protestant Apologist");

    public static IReadOnlyCollection<AgentDescriptor> All { get; } =
    [
        Historian,
        ProtestantApologist
    ];

    public static bool TryGet(
        AgentId agentId,
        out AgentDescriptor descriptor)
    {
        var result = All.FirstOrDefault(
            candidate => candidate.Id == agentId);

        if (result is null)
        {
            descriptor = null!;
            return false;
        }

        descriptor = result;
        return true;
    }
}
EOF

echo "Creating routing contracts..."

cat > src/ApologiaStudio.AgentRuntime/Routing/RoutingDecision.cs <<'EOF'
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Routing;

public sealed record RoutingDecision(
    AgentId AgentId,
    string AgentName,
    string Reason,
    double Confidence,
    bool WasExplicitlyRequested);
EOF

cat > src/ApologiaStudio.AgentRuntime/Routing/IAgentRouter.cs <<'EOF'
using ApologiaStudio.Application.Agents;

namespace ApologiaStudio.AgentRuntime.Routing;

public interface IAgentRouter
{
    RoutingDecision Route(AgentTurnRequest request);
}
EOF

echo "Creating deterministic agent router..."

cat > src/ApologiaStudio.AgentRuntime/Routing/DeterministicAgentRouter.cs <<'EOF'
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
EOF

echo "Creating routing unit tests..."

cat > tests/ApologiaStudio.UnitTests/AgentRuntime/Routing/DeterministicAgentRouterTests.cs <<'EOF'
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Routing;

public sealed class DeterministicAgentRouterTests
{
    private readonly DeterministicAgentRouter _router = new();

    [Fact]
    public void Route_ShouldUseExplicitlyRequestedAgent()
    {
        var request = CreateRequest(
            "Comment défendre la résurrection ?",
            BuiltInAgents.Historian.Id);

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);

        Assert.True(decision.WasExplicitlyRequested);
        Assert.Equal(1.0, decision.Confidence);
    }

    [Fact]
    public void Route_ShouldRejectUnknownRequestedAgent()
    {
        var unknownAgentId = AgentId.New();

        var request = CreateRequest(
            "A question",
            unknownAgentId);

        Assert.Throws<ArgumentException>(
            () => _router.Route(request));
    }

    [Fact]
    public void Route_ShouldSelectHistorianForFrenchHistoricalQuestion()
    {
        var request = CreateRequest(
            "À quelle époque apparaissent les premières preuves historiques de la primauté de Rome ?");

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);

        Assert.False(decision.WasExplicitlyRequested);
    }

    [Fact]
    public void Route_ShouldSelectHistorianForEnglishHistoricalQuestion()
    {
        var request = CreateRequest(
            "When did this doctrine emerge in church history?");

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            decision.AgentId);
    }

    [Fact]
    public void Route_ShouldSelectApologistForFrenchApologeticQuestion()
    {
        var request = CreateRequest(
            "Comment défendre la résurrection face à une objection athée ?");

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);
    }

    [Fact]
    public void Route_ShouldUseApologistAsDefault()
    {
        var request = CreateRequest(
            "Peux-tu m'aider avec ce sujet ?");

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);

        Assert.Equal(0.55, decision.Confidence);
    }

    [Fact]
    public void Route_ShouldUseCurrentMessageInsteadOfEarlierHistory()
    {
        var previousMessage = new ConversationMessageContext(
            MessageId.New(),
            MessageRole.User,
            "Quand cette doctrine est-elle apparue dans l'histoire ?",
            AgentId: null,
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var currentMessageId = MessageId.New();

        var currentMessage = new ConversationMessageContext(
            currentMessageId,
            MessageRole.User,
            "Comment défendre la résurrection contre une objection athée ?",
            AgentId: null,
            DateTimeOffset.UtcNow);

        var request = new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            currentMessageId,
            RequestedAgentId: null,
            History:
            [
                previousMessage,
                currentMessage
            ]);

        var decision = _router.Route(request);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            decision.AgentId);
    }

    private static AgentTurnRequest CreateRequest(
        string content,
        AgentId? requestedAgentId = null)
    {
        var messageId = MessageId.New();

        var message = new ConversationMessageContext(
            messageId,
            MessageRole.User,
            content,
            AgentId: null,
            DateTimeOffset.UtcNow);

        return new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            messageId,
            requestedAgentId,
            History: [message]);
    }
}
EOF

echo "Formatting solution..."

dotnet format ApologiaStudio.sln --no-restore

echo "Running unit tests..."

dotnet test \
  tests/ApologiaStudio.UnitTests/ApologiaStudio.UnitTests.csproj

echo "Building complete solution..."

dotnet build ApologiaStudio.sln --no-restore

echo
echo "Deterministic agent router created successfully."
echo "Expected unit-test total: 14."
