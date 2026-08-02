using ApologiaStudio.Domain.Users;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Abstractions.Conversations;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken);
    Task<Conversation?> GetLatestByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Conversation?>(null);
    }



    void Add(Conversation conversation);
}
