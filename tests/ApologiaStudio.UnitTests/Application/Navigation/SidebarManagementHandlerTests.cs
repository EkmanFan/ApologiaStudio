using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Application.Conversations.DeleteConversation;
using ApologiaStudio.Application.Conversations.MoveConversation;
using ApologiaStudio.Application.Conversations.RestoreConversation;
using ApologiaStudio.Application.Navigation.ReorderPinnedItems;
using ApologiaStudio.Application.Navigation.ReorderProjects;
using ApologiaStudio.Application.Navigation.SetSidebarPin;
using ApologiaStudio.Application.Projects.CreateProject;
using ApologiaStudio.Application.Projects.DeleteProject;
using ApologiaStudio.Application.Projects.RenameProject;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Navigation;

public sealed class SidebarManagementHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProjectHandlers_ShouldCreateRenameAndRejectDuplicateNames()
    {
        var ownerId = UserId.New();
        var repositories = new FakeRepositories();
        var unitOfWork = new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser(ownerId);

        var createHandler = new CreateProjectHandler(
            repositories,
            unitOfWork,
            currentUser,
            new FixedTimeProvider(Now));

        var first = await createHandler.HandleAsync(
            new CreateProjectCommand("  Church history  "),
            CancellationToken.None);

        var second = await createHandler.HandleAsync(
            new CreateProjectCommand("Islam"),
            CancellationToken.None);

        Assert.Equal("Church history", first.Name);
        Assert.Equal(0, first.SortOrder);
        Assert.Equal(1, second.SortOrder);

        var renameHandler = new RenameProjectHandler(
            repositories,
            unitOfWork,
            currentUser);

        await renameHandler.HandleAsync(
            new RenameProjectCommand(first.Id, "Councils"),
            CancellationToken.None);

        Assert.Equal("Councils", first.Name);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => renameHandler.HandleAsync(
                new RenameProjectCommand(first.Id, "islam"),
                CancellationToken.None));
    }

    [Fact]
    public async Task PinHandler_ShouldPinUnpinAndNormalizeOrder()
    {
        var ownerId = UserId.New();
        var conversation = Conversation.Create(
            ownerId,
            "Resurrection",
            Now);

        var project = ConversationProject.Create(
            ownerId,
            "Church history",
            Now);

        var repositories = new FakeRepositories(
            conversations: [conversation],
            projects: [project]);

        var unitOfWork = new FakeUnitOfWork();
        var handler = new SetSidebarPinHandler(
            repositories,
            repositories,
            repositories,
            unitOfWork,
            new FakeCurrentUser(ownerId),
            new FixedTimeProvider(Now));

        await handler.HandleAsync(
            new SetSidebarPinCommand(
                SidebarPinTargetKind.Conversation,
                conversation.Id.Value,
                true),
            CancellationToken.None);

        await handler.HandleAsync(
            new SetSidebarPinCommand(
                SidebarPinTargetKind.Project,
                project.Id.Value,
                true),
            CancellationToken.None);

        Assert.Collection(
            repositories.Pins.OrderBy(pin => pin.SortOrder),
            pin => Assert.Equal(conversation.Id, pin.ConversationId),
            pin => Assert.Equal(project.Id, pin.ProjectId));

        await handler.HandleAsync(
            new SetSidebarPinCommand(
                SidebarPinTargetKind.Conversation,
                conversation.Id.Value,
                false),
            CancellationToken.None);

        var remaining = Assert.Single(repositories.Pins);
        Assert.Equal(project.Id, remaining.ProjectId);
        Assert.Equal(0, remaining.SortOrder);
    }

    [Fact]
    public async Task MoveConversation_ShouldNormalizeSourceAndDestination()
    {
        var ownerId = UserId.New();
        var sourceProject = ConversationProject.Create(
            ownerId,
            "Source",
            Now);

        var destinationProject = ConversationProject.Create(
            ownerId,
            "Destination",
            Now.AddMinutes(1));

        var first = Conversation.Create(ownerId, "First", Now);
        first.MoveToProject(sourceProject);
        first.Reorder(0);

        var second = Conversation.Create(ownerId, "Second", Now.AddMinutes(1));
        second.MoveToProject(sourceProject);
        second.Reorder(1);

        var destination = Conversation.Create(
            ownerId,
            "Already there",
            Now.AddMinutes(2));

        destination.MoveToProject(destinationProject);

        var repositories = new FakeRepositories(
            conversations: [first, second, destination],
            projects: [sourceProject, destinationProject]);

        var handler = new MoveConversationHandler(
            repositories,
            repositories,
            new FakeUnitOfWork(),
            new FakeCurrentUser(ownerId));

        await handler.HandleAsync(
            new MoveConversationCommand(
                first.Id,
                destinationProject.Id,
                1),
            CancellationToken.None);

        Assert.Equal(0, second.SortOrder);
        Assert.Equal(destinationProject.Id, first.ProjectId);
        Assert.Equal(0, destination.SortOrder);
        Assert.Equal(1, first.SortOrder);

        await handler.HandleAsync(
            new MoveConversationCommand(
                first.Id,
                destinationProject.Id,
                0),
            CancellationToken.None);

        Assert.Equal(0, first.SortOrder);
        Assert.Equal(1, destination.SortOrder);
    }

    [Fact]
    public async Task ReorderHandlers_ShouldRequireCompleteOwnedIdentifierSets()
    {
        var ownerId = UserId.New();
        var firstProject = ConversationProject.Create(
            ownerId,
            "First",
            Now,
            0);

        var secondProject = ConversationProject.Create(
            ownerId,
            "Second",
            Now,
            1);

        var firstConversation = Conversation.Create(
            ownerId,
            "First chat",
            Now);

        var secondConversation = Conversation.Create(
            ownerId,
            "Second chat",
            Now);

        var firstPin = SidebarPin.ForConversation(
            firstConversation,
            Now,
            0);

        var secondPin = SidebarPin.ForConversation(
            secondConversation,
            Now,
            1);

        var repositories = new FakeRepositories(
            conversations: [firstConversation, secondConversation],
            projects: [firstProject, secondProject],
            pins: [firstPin, secondPin]);

        var unitOfWork = new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser(ownerId);

        var projectHandler = new ReorderProjectsHandler(
            repositories,
            unitOfWork,
            currentUser);

        await projectHandler.HandleAsync(
            new ReorderProjectsCommand(
                [secondProject.Id, firstProject.Id]),
            CancellationToken.None);

        Assert.Equal(0, secondProject.SortOrder);
        Assert.Equal(1, firstProject.SortOrder);

        await Assert.ThrowsAsync<ArgumentException>(
            () => projectHandler.HandleAsync(
                new ReorderProjectsCommand([firstProject.Id]),
                CancellationToken.None));

        var pinHandler = new ReorderPinnedItemsHandler(
            repositories,
            unitOfWork,
            currentUser);

        await pinHandler.HandleAsync(
            new ReorderPinnedItemsCommand(
                [secondPin.Id, firstPin.Id]),
            CancellationToken.None);

        Assert.Equal(0, secondPin.SortOrder);
        Assert.Equal(1, firstPin.SortOrder);
    }

    [Fact]
    public async Task DeleteAndRestoreConversation_ShouldPreserveContentAndRemovePin()
    {
        var ownerId = UserId.New();
        var conversation = Conversation.Create(
            ownerId,
            "Recoverable",
            Now);

        conversation.AddUserMessage(
            "Preserve this message",
            Now.AddMinutes(1));

        var pin = SidebarPin.ForConversation(
            conversation,
            Now.AddMinutes(2));

        var repositories = new FakeRepositories(
            conversations: [conversation],
            pins: [pin]);

        var unitOfWork = new FakeUnitOfWork();
        var currentUser = new FakeCurrentUser(ownerId);

        var deleteHandler = new DeleteConversationHandler(
            repositories,
            repositories,
            unitOfWork,
            currentUser,
            new FixedTimeProvider(Now.AddMinutes(3)));

        await deleteHandler.HandleAsync(
            new DeleteConversationCommand(conversation.Id),
            CancellationToken.None);

        Assert.True(conversation.IsDeleted);
        Assert.Empty(repositories.Pins);
        Assert.Equal(
            "Preserve this message",
            Assert.Single(conversation.Messages).Content);

        var restoreHandler = new RestoreConversationHandler(
            repositories,
            unitOfWork,
            currentUser);

        await restoreHandler.HandleAsync(
            new RestoreConversationCommand(conversation.Id),
            CancellationToken.None);

        Assert.False(conversation.IsDeleted);
        Assert.Null(conversation.DeletedAt);
        Assert.Empty(repositories.Pins);
    }

    [Fact]
    public async Task DeleteProject_ShouldReturnAllConversationsToChats()
    {
        var ownerId = UserId.New();
        var project = ConversationProject.Create(
            ownerId,
            "Delete me",
            Now,
            0);

        var remainingProject = ConversationProject.Create(
            ownerId,
            "Keep me",
            Now.AddMinutes(1),
            4);

        var active = Conversation.Create(ownerId, "Active", Now);
        active.MoveToProject(project);

        var deleted = Conversation.Create(
            ownerId,
            "Deleted",
            Now);

        deleted.MoveToProject(project);
        deleted.Delete(Now.AddMinutes(1));

        var projectPin = SidebarPin.ForProject(project, Now);

        var repositories = new FakeRepositories(
            conversations: [active, deleted],
            projects: [project, remainingProject],
            pins: [projectPin]);

        var handler = new DeleteProjectHandler(
            repositories,
            repositories,
            repositories,
            new FakeUnitOfWork(),
            new FakeCurrentUser(ownerId));

        await handler.HandleAsync(
            new DeleteProjectCommand(project.Id),
            CancellationToken.None);

        Assert.Null(active.ProjectId);
        Assert.Null(deleted.ProjectId);
        Assert.DoesNotContain(project, repositories.Projects);
        Assert.Empty(repositories.Pins);
        Assert.Equal(0, remainingProject.SortOrder);
    }

    private sealed class FakeRepositories(
        IEnumerable<Conversation>? conversations = null,
        IEnumerable<ConversationProject>? projects = null,
        IEnumerable<SidebarPin>? pins = null)
        : IConversationRepository,
          IConversationProjectRepository,
          ISidebarPinRepository
    {
        public List<Conversation> Conversations { get; } =
            conversations?.ToList() ?? [];

        public List<ConversationProject> Projects { get; } =
            projects?.ToList() ?? [];

        public List<SidebarPin> Pins { get; } =
            pins?.ToList() ?? [];

        public Task<Conversation?> GetByIdAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Conversations.SingleOrDefault(
                    conversation =>
                        conversation.Id == conversationId &&
                        !conversation.IsDeleted));
        }

        public Task<Conversation?> GetByIdIncludingDeletedAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Conversations.SingleOrDefault(
                    conversation => conversation.Id == conversationId));
        }

        public Task<IReadOnlyList<Conversation>> ListByOwnerAsync(
            UserId ownerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Conversation>>(
                Conversations
                    .Where(
                        conversation =>
                            conversation.OwnerId == ownerId &&
                            !conversation.IsDeleted)
                    .ToArray());
        }

        public Task<IReadOnlyList<Conversation>> ListDeletedByOwnerAsync(
            UserId ownerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Conversation>>(
                Conversations
                    .Where(
                        conversation =>
                            conversation.OwnerId == ownerId &&
                            conversation.IsDeleted)
                    .ToArray());
        }

        public void Add(Conversation conversation)
        {
            Conversations.Add(conversation);
        }

        Task<IReadOnlyList<ConversationProject>>
            IConversationProjectRepository.ListByOwnerAsync(
                UserId ownerId,
                CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ConversationProject>>(
                Projects
                    .Where(project => project.OwnerId == ownerId)
                    .ToArray());
        }

        public void Add(ConversationProject project)
        {
            Projects.Add(project);
        }

        public void Remove(ConversationProject project)
        {
            Projects.Remove(project);
        }

        Task<IReadOnlyList<SidebarPin>>
            ISidebarPinRepository.ListByOwnerAsync(
                UserId ownerId,
                CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SidebarPin>>(
                Pins
                    .Where(pin => pin.OwnerId == ownerId)
                    .ToArray());
        }

        public void Add(SidebarPin pin)
        {
            Pins.Add(pin);
        }

        public void Remove(SidebarPin pin)
        {
            Pins.Remove(pin);
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
