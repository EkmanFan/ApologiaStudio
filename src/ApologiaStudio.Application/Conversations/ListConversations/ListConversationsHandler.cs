using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;

namespace ApologiaStudio.Application.Conversations.ListConversations;

public sealed class ListConversationsHandler(
    IConversationRepository conversationRepository,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<ConversationListItem>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var conversations =
            await conversationRepository.ListByOwnerAsync(
                currentUser.UserId,
                cancellationToken);

        return conversations
            .Select(
                conversation =>
                    new ConversationListItem(
                        conversation.Id,
                        conversation.Title,
                        conversation.CreatedAt))
            .ToArray();
    }
}
