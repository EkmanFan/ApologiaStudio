using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Application.Navigation.GetSidebarNavigation;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Navigation;

public sealed class GetSidebarNavigationHandlerTests
{
    [Fact]
    public async Task Handler_ShouldBuildOrderedOwnedNavigation()
    {
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        var project = ConversationProject.Create(
            ownerId,
            "Church history",
            now,
            1);

        var projectConversation = Conversation.Create(
            ownerId,
            "Council of Nicaea",
            now.AddMinutes(-2));

        projectConversation.MoveToProject(project);
        projectConversation.Reorder(2);

        var chat = Conversation.Create(
            ownerId,
            "Resurrection",
            now.AddMinutes(-1));

        chat.Reorder(1);

        var hiddenConversation = Conversation.Create(
            UserId.New(),
            "Another user's conversation",
            now);

        var pin = SidebarPin.ForConversation(
            chat,
            now,
            0);

        var handler = new GetSidebarNavigationHandler(
            new FakeConversationRepository(
                [projectConversation, hiddenConversation, chat]),
            new FakeProjectRepository([project]),
            new FakePinRepository([pin]),
            new FakeCurrentUser(ownerId));

        var result = await handler.HandleAsync(
            CancellationToken.None);

        Assert.Equal(chat.Id, result.DefaultConversationId!.Value);

        var pinned = Assert.Single(result.PinnedItems);
        Assert.Equal(chat.Id.Value, pinned.TargetId);
        Assert.Equal("Resurrection", pinned.Title);

        var projectItem = Assert.Single(result.Projects);
        Assert.Equal("Church history", projectItem.Name);
        Assert.Equal(
            projectConversation.Id,
            Assert.Single(projectItem.Conversations).Id);

        Assert.Equal(chat.Id, Assert.Single(result.Chats).Id);
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
            return Task.FromResult(conversations);
        }

        public void Add(Conversation conversation)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeProjectRepository(
        IReadOnlyList<ConversationProject> projects)
        : IConversationProjectRepository
    {
        public Task<IReadOnlyList<ConversationProject>> ListByOwnerAsync(
            UserId ownerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(projects);
        }

        public void Add(ConversationProject project)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakePinRepository(
        IReadOnlyList<SidebarPin> pins)
        : ISidebarPinRepository
    {
        public Task<IReadOnlyList<SidebarPin>> ListByOwnerAsync(
            UserId ownerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(pins);
        }

        public void Add(SidebarPin pin)
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
