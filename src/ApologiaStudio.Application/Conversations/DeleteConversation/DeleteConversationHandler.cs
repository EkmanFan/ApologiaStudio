using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Navigation;

namespace ApologiaStudio.Application.Conversations.DeleteConversation;

public sealed class DeleteConversationHandler(
    IConversationRepository conversationRepository,
    ISidebarPinRepository pinRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(
        DeleteConversationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var conversation = await conversationRepository.GetByIdAsync(
            command.ConversationId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Conversation '{command.ConversationId}' was not found.");

        if (conversation.OwnerId != currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "The current user cannot delete this conversation.");
        }

        var sourceProjectId = conversation.ProjectId;
        conversation.Delete(timeProvider.GetUtcNow());

        var conversations =
            await conversationRepository.ListByOwnerAsync(
                currentUser.UserId,
                cancellationToken);

        SidebarOrdering.AssignConversationOrder(
            SidebarOrdering.OrderConversations(
                conversations.Where(
                    candidate =>
                        !candidate.IsDeleted &&
                        candidate.ProjectId == sourceProjectId)));

        var pins = await pinRepository.ListByOwnerAsync(
            currentUser.UserId,
            cancellationToken);

        foreach (var pin in pins.Where(
                     pin => pin.ConversationId == conversation.Id).ToArray())
        {
            pinRepository.Remove(pin);
        }

        SidebarOrdering.AssignPinOrder(
            SidebarOrdering.OrderPins(
                pins.Where(
                    pin => pin.ConversationId != conversation.Id)));

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
