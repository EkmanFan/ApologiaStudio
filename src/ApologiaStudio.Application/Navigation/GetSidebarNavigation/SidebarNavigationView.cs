using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Navigation.GetSidebarNavigation;

public sealed record SidebarNavigationView(
    ConversationId? DefaultConversationId,
    IReadOnlyList<SidebarPinnedItem> PinnedItems,
    IReadOnlyList<SidebarProjectItem> Projects,
    IReadOnlyList<SidebarConversationItem> Chats)
{
    public static SidebarNavigationView Empty { get; } =
        new(
            null,
            Array.Empty<SidebarPinnedItem>(),
            Array.Empty<SidebarProjectItem>(),
            Array.Empty<SidebarConversationItem>());
}
