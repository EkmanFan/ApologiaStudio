using System.Runtime.CompilerServices;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Preferences;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.Conversations.SendMessage;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Conversations;

public sealed class SendMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldPersistUserAndAgentMessages()
    {
        var ownerId = UserId.New();
        var historianId = AgentId.New();

        var conversation = Conversation.Create(
            ownerId,
            "Historical question",
            DateTimeOffset.UtcNow);

        var repository = new FakeConversationRepository(conversation);

        var runtime = new FakeAgentRuntime(
            historianId,
            "The historical evidence begins...");

        var unitOfWork = new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser(ownerId);

        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(
                2026,
                8,
                2,
                12,
                0,
                0,
                TimeSpan.Zero));

        var handler = new SendMessageHandler(
            repository,
            runtime,
            unitOfWork,
            new FakeUserPreferencesRepository(
                UserPreferences.Create(
                    ownerId,
                    ApplicationLanguage.English,
                    theologicalLanguage: null,
                    updatedAt: DateTimeOffset.UtcNow)),
            currentUser,
            timeProvider);

        var command = new SendMessageCommand(
            conversation.Id,
            "When did this doctrine emerge?");

        var events = new List<AgentRunEvent>();

        await foreach (var agentEvent in handler.HandleAsync(
                           command,
                           CancellationToken.None))
        {
            events.Add(agentEvent);
        }

        Assert.Equal(2, conversation.Messages.Count);

        var userMessage = conversation.Messages[0];

        Assert.Equal(MessageRole.User, userMessage.Role);
        Assert.Equal(
            "When did this doctrine emerge?",
            userMessage.Content);

        var agentMessage = conversation.Messages[1];

        Assert.Equal(MessageRole.Agent, agentMessage.Role);
        Assert.Equal(historianId, agentMessage.AgentId);
        Assert.Equal(
            "The historical evidence begins...",
            agentMessage.Content);

        Assert.Equal(2, unitOfWork.SaveCount);
        Assert.Equal(3, events.Count);
        Assert.IsType<AgentTurnCompletedEvent>(events[^1]);
        Assert.Equal(
            ApplicationLanguage.English,
            runtime.Request?.TheologicalLanguage);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectAnotherUsersConversation()
    {
        var ownerId = UserId.New();
        var anotherUserId = UserId.New();

        var conversation = Conversation.Create(
            ownerId,
            "Private conversation",
            DateTimeOffset.UtcNow);

        var handler = new SendMessageHandler(
            new FakeConversationRepository(conversation),
            new FakeAgentRuntime(
                AgentId.New(),
                "This response must not be produced."),
            new FakeUnitOfWork(),
            new FakeUserPreferencesRepository(),
            new FakeCurrentUser(anotherUserId),
            TimeProvider.System);

        var command = new SendMessageCommand(
            conversation.Id,
            "Unauthorized message");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () =>
            {
                await foreach (var _ in handler.HandleAsync(
                                   command,
                                   CancellationToken.None))
                {
                }
            });

        Assert.Empty(conversation.Messages);
    }

    private sealed class FakeConversationRepository(
        Conversation conversation)
        : IConversationRepository
    {
        public Task<Conversation?> GetByIdAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken)
        {
            Conversation? result = conversation.Id == conversationId
                ? conversation
                : null;

            return Task.FromResult(result);
        }

        public void Add(Conversation newConversation)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeAgentRuntime(
        AgentId agentId,
        string response)
        : IAgentRuntime
    {
        public AgentTurnRequest? Request { get; private set; }

        public async IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
            AgentTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Request = request;

            yield return new AgentSelectedEvent(
                agentId,
                "Historian",
                "The question concerns historical development.");

            await Task.Yield();

            cancellationToken.ThrowIfCancellationRequested();

            yield return new TextDeltaEvent(response);

            yield return new AgentTurnCompletedEvent(
                agentId,
                response);
        }
    }

    private sealed class FakeUserPreferencesRepository(
        UserPreferences? preferences = null)
        : IUserPreferencesRepository
    {
        public Task<UserPreferences?> GetAsync(
            UserId userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                preferences?.UserId == userId
                    ? preferences
                    : null);
        }

        public void Add(UserPreferences newPreferences)
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

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
