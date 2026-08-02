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

echo "Creating runtime directories..."

mkdir -p \
  src/ApologiaStudio.AgentRuntime/Execution \
  tests/ApologiaStudio.UnitTests/AgentRuntime/Execution \
  tests/ApologiaStudio.UnitTests/Application/Conversations

echo "Creating simulated response provider..."

cat > src/ApologiaStudio.AgentRuntime/Execution/SimulatedAgentResponseProvider.cs <<'EOF'
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class SimulatedAgentResponseProvider
{
    public string CreateResponse(
        AgentId agentId,
        string userMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        if (agentId == BuiltInAgents.Historian.Id)
        {
            return
                "Historian simulation: I would examine the chronology, " +
                "primary sources and historical development relevant to: " +
                $"\"{userMessage}\"";
        }

        if (agentId == BuiltInAgents.ProtestantApologist.Id)
        {
            return
                "Protestant apologist simulation: I would clarify the claim, " +
                "examine the biblical basis and construct a reasoned defence concerning: " +
                $"\"{userMessage}\"";
        }

        throw new ArgumentException(
            $"Agent '{agentId}' is not supported by the simulated runtime.",
            nameof(agentId));
    }
}
EOF

echo "Creating simulated agent runtime..."

cat > src/ApologiaStudio.AgentRuntime/Execution/SimulatedAgentRuntime.cs <<'EOF'
using System.Runtime.CompilerServices;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class SimulatedAgentRuntime(
    IAgentRouter agentRouter,
    SimulatedAgentResponseProvider responseProvider)
    : IAgentRuntime
{
    public async IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
        AgentTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var routingDecision = agentRouter.Route(request);

        yield return new AgentSelectedEvent(
            routingDecision.AgentId,
            routingDecision.AgentName,
            routingDecision.Reason);

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var userMessage = FindCurrentUserMessage(request);

        var completeResponse = responseProvider.CreateResponse(
            routingDecision.AgentId,
            userMessage);

        foreach (var chunk in SplitIntoChunks(
                     completeResponse,
                     maximumChunkLength: 48))
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return new TextDeltaEvent(chunk);

            await Task.Yield();
        }

        yield return new AgentTurnCompletedEvent(
            routingDecision.AgentId,
            completeResponse);
    }

    private static string FindCurrentUserMessage(
        AgentTurnRequest request)
    {
        var currentMessage = request.History.FirstOrDefault(
            message =>
                message.MessageId == request.UserMessageId &&
                message.Role == MessageRole.User);

        if (currentMessage is null)
        {
            throw new InvalidOperationException(
                "The current user message was not found in the conversation history.");
        }

        return currentMessage.Content;
    }

    private static IEnumerable<string> SplitIntoChunks(
        string content,
        int maximumChunkLength)
    {
        if (maximumChunkLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumChunkLength));
        }

        for (var position = 0;
             position < content.Length;
             position += maximumChunkLength)
        {
            var length = Math.Min(
                maximumChunkLength,
                content.Length - position);

            yield return content.Substring(
                position,
                length);
        }
    }
}
EOF

echo "Creating simulated runtime tests..."

cat > tests/ApologiaStudio.UnitTests/AgentRuntime/Execution/SimulatedAgentRuntimeTests.cs <<'EOF'
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Execution;

public sealed class SimulatedAgentRuntimeTests
{
    private readonly SimulatedAgentRuntime _runtime = new(
        new DeterministicAgentRouter(),
        new SimulatedAgentResponseProvider());

    [Fact]
    public async Task RunTurnAsync_ShouldRouteHistoricalQuestionToHistorian()
    {
        var request = CreateRequest(
            "Quand cette doctrine est-elle apparue dans l'histoire ?");

        var events = await CollectEventsAsync(request);

        var selected = Assert.IsType<AgentSelectedEvent>(
            events[0]);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            selected.AgentId);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            completed.AgentId);

        Assert.Contains(
            "Historian simulation",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldRouteApologeticQuestionToApologist()
    {
        var request = CreateRequest(
            "Comment défendre la résurrection face à une objection athée ?");

        var events = await CollectEventsAsync(request);

        var selected = Assert.IsType<AgentSelectedEvent>(
            events[0]);

        Assert.Equal(
            BuiltInAgents.ProtestantApologist.Id,
            selected.AgentId);

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Contains(
            "Protestant apologist simulation",
            completed.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldRespectExplicitAgentSelection()
    {
        var request = CreateRequest(
            "Comment défendre la résurrection ?",
            BuiltInAgents.Historian.Id);

        var events = await CollectEventsAsync(request);

        var selected = Assert.IsType<AgentSelectedEvent>(
            events[0]);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            selected.AgentId);

        Assert.Contains(
            "explicitly selected",
            selected.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldEmitTextDeltaEvents()
    {
        var request = CreateRequest(
            "Quand cette doctrine est-elle apparue ?");

        var events = await CollectEventsAsync(request);

        var textEvents = events
            .OfType<TextDeltaEvent>()
            .ToArray();

        Assert.NotEmpty(textEvents);

        var streamedText = string.Concat(
            textEvents.Select(agentEvent => agentEvent.Content));

        var completed = Assert.IsType<AgentTurnCompletedEvent>(
            events[^1]);

        Assert.Equal(
            completed.Content,
            streamedText);
    }

    private async Task<List<AgentRunEvent>> CollectEventsAsync(
        AgentTurnRequest request)
    {
        var events = new List<AgentRunEvent>();

        await foreach (var agentEvent in _runtime.RunTurnAsync(
                           request,
                           CancellationToken.None))
        {
            events.Add(agentEvent);
        }

        return events;
    }

    private static AgentTurnRequest CreateRequest(
        string content,
        AgentId? requestedAgentId = null)
    {
        var messageId = MessageId.New();

        return new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            messageId,
            requestedAgentId,
            History:
            [
                new ConversationMessageContext(
                    messageId,
                    MessageRole.User,
                    content,
                    AgentId: null,
                    DateTimeOffset.UtcNow)
            ]);
    }
}
EOF

echo "Creating full application-runtime integration unit test..."

cat > tests/ApologiaStudio.UnitTests/Application/Conversations/SendMessageWithSimulatedRuntimeTests.cs <<'EOF'
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Conversations.SendMessage;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Conversations;

public sealed class SendMessageWithSimulatedRuntimeTests
{
    [Fact]
    public async Task HandleAsync_ShouldRouteAndPersistHistorianResponse()
    {
        var ownerId = UserId.New();

        var conversation = Conversation.Create(
            ownerId,
            "Historical discussion",
            DateTimeOffset.UtcNow);

        var repository = new FakeConversationRepository(
            conversation);

        var runtime = new SimulatedAgentRuntime(
            new DeterministicAgentRouter(),
            new SimulatedAgentResponseProvider());

        var unitOfWork = new FakeUnitOfWork();

        var handler = new SendMessageHandler(
            repository,
            runtime,
            unitOfWork,
            new FakeCurrentUser(ownerId),
            TimeProvider.System);

        var command = new SendMessageCommand(
            conversation.Id,
            "Quand apparaissent les premières preuves historiques de cette doctrine ?");

        await foreach (var _ in handler.HandleAsync(
                           command,
                           CancellationToken.None))
        {
        }

        Assert.Equal(
            2,
            conversation.Messages.Count);

        var agentMessage = conversation.Messages[1];

        Assert.Equal(
            MessageRole.Agent,
            agentMessage.Role);

        Assert.Equal(
            BuiltInAgents.Historian.Id,
            agentMessage.AgentId);

        Assert.Contains(
            "Historian simulation",
            agentMessage.Content,
            StringComparison.Ordinal);

        Assert.Equal(
            2,
            unitOfWork.SaveCount);
    }

    private sealed class FakeConversationRepository(
        Conversation conversation)
        : IConversationRepository
    {
        public Task<Conversation?> GetByIdAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken)
        {
            Conversation? result =
                conversation.Id == conversationId
                    ? conversation
                    : null;

            return Task.FromResult(result);
        }

        public void Add(Conversation newConversation)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser(UserId userId)
        : ICurrentUser
    {
        public UserId UserId { get; } = userId;
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
echo "Simulated agent runtime created successfully."
echo "Expected unit-test total: 19."
