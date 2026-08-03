using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Navigation.GetSidebarNavigation;

public sealed class GetSidebarNavigationHandler(
    IConversationRepository conversationRepository,
    IConversationProjectRepository projectRepository,
    ISidebarPinRepository pinRepository,
    ICurrentUser currentUser)
{
    public async Task<SidebarNavigationView> HandleAsync(
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;

        var conversations =
            await conversationRepository.ListByOwnerAsync(
                ownerId,
                cancellationToken);

        var deletedConversations =
            await conversationRepository.ListDeletedByOwnerAsync(
                ownerId,
                cancellationToken);

        var projects =
            await projectRepository.ListByOwnerAsync(
                ownerId,
                cancellationToken);

        var pins =
            await pinRepository.ListByOwnerAsync(
                ownerId,
                cancellationToken);

        var ownedConversations = conversations
            .Where(
                conversation =>
                    conversation.OwnerId == ownerId &&
                    !conversation.IsDeleted)
            .ToArray();

        var ownedProjects = projects
            .Where(project => project.OwnerId == ownerId)
            .ToArray();

        var conversationById =
            ownedConversations.ToDictionary(
                conversation => conversation.Id);

        var projectById =
            ownedProjects.ToDictionary(
                project => project.Id);

        var projectItems = ownedProjects
            .OrderBy(project => project.SortOrder)
            .ThenBy(project => project.CreatedAt)
            .Select(
                project =>
                    new SidebarProjectItem(
                        project.Id,
                        project.Name,
                        project.SortOrder,
                        MapConversations(
                            ownedConversations.Where(
                                conversation =>
                                    conversation.ProjectId == project.Id))))
            .ToArray();

        var chats = MapConversations(
            ownedConversations.Where(
                conversation => conversation.ProjectId is null));

        var pinnedItems = pins
            .Where(pin => pin.OwnerId == ownerId)
            .OrderBy(pin => pin.SortOrder)
            .ThenBy(pin => pin.CreatedAt)
            .Select(
                pin => MapPin(
                    pin,
                    conversationById,
                    projectById))
            .OfType<SidebarPinnedItem>()
            .ToArray();

        var defaultConversationId = ownedConversations
            .OrderByDescending(
                conversation => conversation.CreatedAt)
            .Select(
                conversation =>
                    (ConversationId?)conversation.Id)
            .FirstOrDefault();

        var deletedChats = deletedConversations
            .Where(
                conversation =>
                    conversation.OwnerId == ownerId &&
                    conversation.DeletedAt.HasValue)
            .OrderByDescending(
                conversation => conversation.DeletedAt)
            .ThenByDescending(
                conversation => conversation.CreatedAt)
            .Select(
                conversation =>
                    new SidebarDeletedConversationItem(
                        conversation.Id,
                        conversation.Title,
                        conversation.DeletedAt!.Value))
            .ToArray();

        return new SidebarNavigationView(
            defaultConversationId,
            pinnedItems,
            projectItems,
            chats,
            deletedChats);
    }

    private static IReadOnlyList<SidebarConversationItem>
        MapConversations(
            IEnumerable<Conversation> conversations)
    {
        return conversations
            .OrderBy(conversation => conversation.SortOrder)
            .ThenByDescending(conversation => conversation.CreatedAt)
            .Select(
                conversation =>
                    new SidebarConversationItem(
                        conversation.Id,
                        conversation.Title,
                        conversation.CreatedAt,
                        conversation.SortOrder))
            .ToArray();
    }

    private static SidebarPinnedItem? MapPin(
        SidebarPin pin,
        IReadOnlyDictionary<ConversationId, Conversation>
            conversationById,
        IReadOnlyDictionary<ConversationProjectId, ConversationProject>
            projectById)
    {
        if (pin.TargetKind == SidebarPinTargetKind.Conversation &&
            pin.ConversationId is { } conversationId &&
            conversationById.TryGetValue(
                conversationId,
                out var conversation))
        {
            return new SidebarPinnedItem(
                pin.Id,
                pin.TargetKind,
                conversationId.Value,
                conversation.Title,
                pin.SortOrder);
        }

        if (pin.TargetKind == SidebarPinTargetKind.Project &&
            pin.ProjectId is { } projectId &&
            projectById.TryGetValue(
                projectId,
                out var project))
        {
            return new SidebarPinnedItem(
                pin.Id,
                pin.TargetKind,
                projectId.Value,
                project.Name,
                pin.SortOrder);
        }

        return null;
    }
}
