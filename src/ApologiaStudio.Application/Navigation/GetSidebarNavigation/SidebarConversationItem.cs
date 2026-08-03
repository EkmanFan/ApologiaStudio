using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Navigation.GetSidebarNavigation;

public sealed record SidebarConversationItem(
    ConversationId Id,
    string Title,
    DateTimeOffset CreatedAt,
    int SortOrder);
