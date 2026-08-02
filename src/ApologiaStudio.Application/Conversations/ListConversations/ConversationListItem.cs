using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Conversations.ListConversations;

public sealed record ConversationListItem(
    ConversationId Id,
    string Title,
    DateTimeOffset CreatedAt);
