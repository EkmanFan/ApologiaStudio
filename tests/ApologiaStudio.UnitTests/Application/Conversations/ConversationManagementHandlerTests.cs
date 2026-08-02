using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Conversations.GetConversation;
using ApologiaStudio.Application.Conversations.ListConversations;
using ApologiaStudio.Application.Conversations.RenameConversation;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Conversations;

public sealed class ConversationManagementHandlerTests
{
    [Fact]
    public async Task ListHandler_ShouldMapOwnedConversations()
    {
        var userId = UserId.New();

        var first = Conversation.Create(
            userId,
            "First",
            DateTimeOffset.UtcNow.AddMinutes(-2));

        var second = Conversation.Create(
            userId,
            "Second",
            DateTimeOffset.UtcNow.AddMinutes(-1));

        var repository = new FakeConversationRepository(
            [second, first]);

        var handler = new ListConversationsHandler(
            repository,
            new FakeCurrentUser(userId));

        var result = await handler.HandleAsync(
            CancellationToken.None);

        Assert.Collection(
            result,
            item =>
            {
                Assert.Equal(second.Id, item.Id);
                Assert.Equal("Second", item.Title);
            },
            item =>
            {
                Assert.Equal(first.Id, item.Id);
                Assert.Equal("First", item.Title);
            });
    }

    [Fact]
    public async Task GetHandler_ShouldReturnOwnedConversation()
    {
        var userId = UserId.New();

        var conversation = Conversation.Create(
            userId,
            "Owned",
            DateTimeOffset.UtcNow);

        var handler = new GetConversationHandler(
            new FakeConversationRepository(
                [conversation]),
            new FakeCurrentUser(userId));

        var result = await handler.HandleAsync(
            conversation.Id,
            CancellationToken.None);

        Assert.Same(conversation, result);
    }

    [Fact]
    public async Task GetHandler_ShouldHideAnotherUsersConversation()
    {
        var conversation = Conversation.Create(
            UserId.New(),
            "Private",
            DateTimeOffset.UtcNow);

        var handler = new GetConversationHandler(
            new FakeConversationRepository(
                [conversation]),
            new FakeCurrentUser(UserId.New()));

        var result = await handler.HandleAsync(
            conversation.Id,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RenameHandler_ShouldRenameOwnedConversation()
    {
        var userId = UserId.New();

        var conversation = Conversation.Create(
            userId,
            "Old title",
            DateTimeOffset.UtcNow);

        var unitOfWork = new FakeUnitOfWork();

        var handler = new RenameConversationHandler(
            new FakeConversationRepository(
                [conversation]),
            unitOfWork,
            new FakeCurrentUser(userId));

        await handler.HandleAsync(
            new RenameConversationCommand(
                conversation.Id,
                "New title"),
            CancellationToken.None);

        Assert.Equal(
            "New title",
            conversation.Title);

        Assert.Equal(
            1,
            unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RenameHandler_ShouldRejectAnotherUser()
    {
        var conversation = Conversation.Create(
            UserId.New(),
            "Private",
            DateTimeOffset.UtcNow);

        var unitOfWork = new FakeUnitOfWork();

        var handler = new RenameConversationHandler(
            new FakeConversationRepository(
                [conversation]),
            unitOfWork,
            new FakeCurrentUser(UserId.New()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(
                new RenameConversationCommand(
                    conversation.Id,
                    "Forbidden"),
                CancellationToken.None));

        Assert.Equal(
            "Private",
            conversation.Title);

        Assert.Equal(
            0,
            unitOfWork.SaveCount);
    }

    private sealed class FakeConversationRepository(
        IReadOnlyList<Conversation> conversations)
        : IConversationRepository
    {
        public Task<Conversation?> GetByIdAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                conversations.FirstOrDefault(
                    conversation =>
                        conversation.Id == conversationId));
        }

        public Task<IReadOnlyList<Conversation>> ListByOwnerAsync(
            UserId ownerId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Conversation> result =
                conversations
                    .Where(
                        conversation =>
                            conversation.OwnerId == ownerId)
                    .ToArray();

            return Task.FromResult(result);
        }

        public void Add(
            Conversation conversation)
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
