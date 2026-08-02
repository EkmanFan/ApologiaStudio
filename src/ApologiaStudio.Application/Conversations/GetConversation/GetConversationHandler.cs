using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Conversations.GetConversation;

public sealed class GetConversationHandler(
    IConversationRepository conversationRepository,
    ICurrentUser currentUser)
{
    public async Task<Conversation?> HandleAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        var conversation =
            await conversationRepository.GetByIdAsync(
                conversationId,
                cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        return conversation.OwnerId == currentUser.UserId
            ? conversation
            : null;
    }
}
