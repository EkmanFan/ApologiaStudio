using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Preferences;
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
            new FakeUserPreferencesRepository(),
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

    private sealed class FakeUserPreferencesRepository
        : IUserPreferencesRepository
    {
        public Task<UserPreferences?> GetAsync(
            UserId userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<UserPreferences?>(null);
        }

        public void Add(UserPreferences preferences)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeCurrentUser(UserId userId)
        : ICurrentUser
    {
        public UserId UserId { get; } = userId;
    }
}
