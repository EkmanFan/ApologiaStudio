using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;

namespace ApologiaStudio.Application.Conversations.RenameConversation;

public sealed class RenameConversationHandler(
    IConversationRepository conversationRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(
        RenameConversationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var conversation =
            await conversationRepository.GetByIdAsync(
                command.ConversationId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Conversation '{command.ConversationId}' was not found.");

        if (conversation.OwnerId != currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "The current user cannot rename this conversation.");
        }

        conversation.Rename(command.Title);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);
    }
}
