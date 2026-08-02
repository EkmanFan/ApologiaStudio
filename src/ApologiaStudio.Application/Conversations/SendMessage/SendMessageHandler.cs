using System.Runtime.CompilerServices;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Preferences;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Conversations.SendMessage;

public sealed class SendMessageHandler(
    IConversationRepository conversationRepository,
    IAgentRuntime agentRuntime,
    IUnitOfWork unitOfWork,
    IUserPreferencesRepository preferencesRepository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async IAsyncEnumerable<AgentRunEvent> HandleAsync(
        SendMessageCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var conversation = await conversationRepository.GetByIdAsync(
            command.ConversationId,
            cancellationToken);

        if (conversation is null)
        {
            throw new KeyNotFoundException(
                $"Conversation '{command.ConversationId}' was not found.");
        }

        if (conversation.OwnerId != currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "The current user cannot access this conversation.");
        }

        var userMessage = conversation.AddUserMessage(
            command.Content,
            timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var history = conversation.Messages
            .Select(message => new ConversationMessageContext(
                message.Id,
                message.Role,
                message.Content,
                message.AgentId,
                message.CreatedAt))
            .ToArray();

        var preferences = await preferencesRepository.GetAsync(
            currentUser.UserId,
            cancellationToken);

        var theologicalLanguage =
            preferences?.EffectiveTheologicalLanguage ??
            UserPreferences.DefaultInterfaceLanguage;

        var request = new AgentTurnRequest(
            conversation.Id,
            currentUser.UserId,
            userMessage.Id,
            command.RequestedAgentId,
            history,
            theologicalLanguage);

        await foreach (var agentEvent in agentRuntime
                           .RunTurnAsync(request, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            if (agentEvent is AgentTurnCompletedEvent completed)
            {
                conversation.AddAgentMessage(
                    completed.AgentId,
                    completed.Content,
                    timeProvider.GetUtcNow());

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            yield return agentEvent;
        }
    }
}
