using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Repositories;

public sealed class EfConversationRepository(
    ApologiaStudioDbContext dbContext)
    : IConversationRepository
{
    public Task<Conversation?> GetByIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Conversations
            .Include(
                conversation =>
                    conversation.Messages.OrderBy(
                        message => message.CreatedAt))
            .SingleOrDefaultAsync(
                conversation =>
                    conversation.Id == conversationId,
                cancellationToken);
    }

    public Task<Conversation?> GetLatestByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        return dbContext.Conversations
            .Include(
                conversation =>
                    conversation.Messages.OrderBy(
                        message => message.CreatedAt))
            .Where(
                conversation =>
                    conversation.OwnerId == ownerId)
            .OrderByDescending(
                conversation =>
                    conversation.CreatedAt)
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> ListByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Conversations
            .AsNoTracking()
            .Where(
                conversation =>
                    conversation.OwnerId == ownerId)
            .OrderByDescending(
                conversation =>
                    conversation.CreatedAt)
            .ToListAsync(
                cancellationToken);
    }

    public void Add(
        Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        dbContext.Conversations.Add(conversation);
    }
}
