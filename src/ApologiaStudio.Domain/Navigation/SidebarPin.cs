using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Domain.Navigation;

public sealed class SidebarPin
{
    private SidebarPin(
        SidebarPinId id,
        UserId ownerId,
        SidebarPinTargetKind targetKind,
        ConversationId? conversationId,
        ConversationProjectId? projectId,
        DateTimeOffset createdAt,
        int sortOrder)
    {
        Id = id;
        OwnerId = ownerId;
        TargetKind = targetKind;
        ConversationId = conversationId;
        ProjectId = projectId;
        CreatedAt = createdAt;
        SortOrder = sortOrder;
    }

    public SidebarPinId Id { get; }

    public UserId OwnerId { get; }

    public SidebarPinTargetKind TargetKind { get; }

    public ConversationId? ConversationId { get; }

    public ConversationProjectId? ProjectId { get; }

    public DateTimeOffset CreatedAt { get; }

    public int SortOrder { get; private set; }

    public static SidebarPin ForConversation(
        Conversation conversation,
        DateTimeOffset createdAt,
        int sortOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ValidateSortOrder(sortOrder);

        return new SidebarPin(
            SidebarPinId.New(),
            conversation.OwnerId,
            SidebarPinTargetKind.Conversation,
            conversation.Id,
            null,
            createdAt,
            sortOrder);
    }

    public static SidebarPin ForProject(
        ConversationProject project,
        DateTimeOffset createdAt,
        int sortOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(project);
        ValidateSortOrder(sortOrder);

        return new SidebarPin(
            SidebarPinId.New(),
            project.OwnerId,
            SidebarPinTargetKind.Project,
            null,
            project.Id,
            createdAt,
            sortOrder);
    }

    public void Reorder(int sortOrder)
    {
        ValidateSortOrder(sortOrder);
        SortOrder = sortOrder;
    }

    private static void ValidateSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "Pinned item sort order cannot be negative.");
        }
    }
}
