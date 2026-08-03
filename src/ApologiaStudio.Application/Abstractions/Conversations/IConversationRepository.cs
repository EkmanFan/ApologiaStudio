using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Abstractions.Conversations;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken);

    Task<Conversation?> GetByIdIncludingDeletedAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        return GetByIdAsync(
            conversationId,
            cancellationToken);
    }

    Task<Conversation?> GetLatestByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Conversation?>(null);
    }

    Task<IReadOnlyList<Conversation>> ListByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Conversation>>(
            Array.Empty<Conversation>());
    }

    Task<IReadOnlyList<Conversation>> ListDeletedByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Conversation>>(
            Array.Empty<Conversation>());
    }

    void Add(Conversation conversation);
}
