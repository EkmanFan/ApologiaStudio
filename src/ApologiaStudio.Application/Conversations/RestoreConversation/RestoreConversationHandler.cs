using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Navigation;

namespace ApologiaStudio.Application.Conversations.RestoreConversation;

public sealed class RestoreConversationHandler(
    IConversationRepository conversationRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(
        RestoreConversationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var conversation =
            await conversationRepository.GetByIdIncludingDeletedAsync(
                command.ConversationId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Conversation '{command.ConversationId}' was not found.");

        if (conversation.OwnerId != currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "The current user cannot restore this conversation.");
        }

        if (!conversation.IsDeleted)
        {
            return;
        }

        var activeConversations =
            await conversationRepository.ListByOwnerAsync(
                currentUser.UserId,
                cancellationToken);

        var destination = SidebarOrdering.OrderConversations(
                activeConversations.Where(
                    candidate =>
                        candidate.ProjectId == conversation.ProjectId))
            .ToList();

        conversation.Restore();
        destination.Add(conversation);
        SidebarOrdering.AssignConversationOrder(destination);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
