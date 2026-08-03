using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Navigation;

internal static class SidebarOrdering
{
    public static IReadOnlyList<Conversation> OrderConversations(
        IEnumerable<Conversation> conversations)
    {
        return conversations
            .OrderBy(conversation => conversation.SortOrder)
            .ThenByDescending(conversation => conversation.CreatedAt)
            .ThenBy(conversation => conversation.Id.Value)
            .ToArray();
    }

    public static IReadOnlyList<ConversationProject> OrderProjects(
        IEnumerable<ConversationProject> projects)
    {
        return projects
            .OrderBy(project => project.SortOrder)
            .ThenBy(project => project.CreatedAt)
            .ThenBy(project => project.Id.Value)
            .ToArray();
    }

    public static IReadOnlyList<SidebarPin> OrderPins(
        IEnumerable<SidebarPin> pins)
    {
        return pins
            .OrderBy(pin => pin.SortOrder)
            .ThenBy(pin => pin.CreatedAt)
            .ThenBy(pin => pin.Id.Value)
            .ToArray();
    }

    public static void AssignConversationOrder(
        IReadOnlyList<Conversation> conversations)
    {
        for (var index = 0; index < conversations.Count; index++)
        {
            conversations[index].Reorder(index);
        }
    }

    public static void AssignProjectOrder(
        IReadOnlyList<ConversationProject> projects)
    {
        for (var index = 0; index < projects.Count; index++)
        {
            projects[index].Reorder(index);
        }
    }

    public static void AssignPinOrder(
        IReadOnlyList<SidebarPin> pins)
    {
        for (var index = 0; index < pins.Count; index++)
        {
            pins[index].Reorder(index);
        }
    }
}
