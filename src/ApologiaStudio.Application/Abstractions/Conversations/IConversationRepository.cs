using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Abstractions.Conversations;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken);

    void Add(Conversation conversation);
}
