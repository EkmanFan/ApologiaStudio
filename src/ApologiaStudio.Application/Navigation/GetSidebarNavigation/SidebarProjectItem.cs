using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Navigation.GetSidebarNavigation;

public sealed record SidebarProjectItem(
    ConversationProjectId Id,
    string Name,
    int SortOrder,
    IReadOnlyList<SidebarConversationItem> Conversations);
