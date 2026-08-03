using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Navigation.GetSidebarNavigation;

public sealed record SidebarDeletedConversationItem(
    ConversationId Id,
    string Title,
    DateTimeOffset DeletedAt);
