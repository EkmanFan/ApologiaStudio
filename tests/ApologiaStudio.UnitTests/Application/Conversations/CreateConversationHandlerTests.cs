using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Conversations.CreateConversation;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Conversations;

public sealed class CreateConversationHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldCreateAndStoreConversation()
    {
        var userId = UserId.New();
        var repository = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();

        var now = new DateTimeOffset(
            2026,
            8,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);

        var handler = new CreateConversationHandler(
            repository,
            unitOfWork,
            new FakeCurrentUser(userId),
            new FixedTimeProvider(now));

        var conversation = await handler.HandleAsync(
            new CreateConversationCommand(
                "First discussion"),
            CancellationToken.None);

        Assert.Equal(
            userId,
            conversation.OwnerId);

        Assert.Equal(
            "First discussion",
            conversation.Title);

        Assert.Equal(
            now,
            conversation.CreatedAt);

        Assert.Same(
            conversation,
            repository.StoredConversation);

        Assert.Equal(
            1,
            unitOfWork.SaveCount);
    }

    private sealed class FakeConversationRepository
        : IConversationRepository
    {
        public Conversation? StoredConversation { get; private set; }

        public Task<Conversation?> GetByIdAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken)
        {
            var result =
                StoredConversation?.Id == conversationId
                    ? StoredConversation
                    : null;

            return Task.FromResult(result);
        }

        public void Add(Conversation conversation)
        {
            StoredConversation = conversation;
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
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
    }
}
