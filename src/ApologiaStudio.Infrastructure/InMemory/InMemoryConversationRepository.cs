using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Infrastructure.InMemory;

public sealed class InMemoryConversationRepository
    : IConversationRepository
{
    private readonly Dictionary<ConversationId, Conversation>
        _conversations = [];

    public Task<Conversation?> GetByIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _conversations.TryGetValue(
            conversationId,
            out var conversation);

        return Task.FromResult(conversation);
    }

    public void Add(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (!_conversations.TryAdd(
                conversation.Id,
                conversation))
        {
            throw new InvalidOperationException(
                $"Conversation '{conversation.Id}' already exists.");
        }
    }
}
