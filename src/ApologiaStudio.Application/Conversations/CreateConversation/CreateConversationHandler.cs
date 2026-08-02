using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Conversations.CreateConversation;

public sealed class CreateConversationHandler(
    IConversationRepository conversationRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Conversation> HandleAsync(
        CreateConversationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var conversation = Conversation.Create(
            currentUser.UserId,
            command.Title,
            timeProvider.GetUtcNow());

        conversationRepository.Add(conversation);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return conversation;
    }
}
