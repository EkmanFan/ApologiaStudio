using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Conversations.DeleteConversation;

public sealed record DeleteConversationCommand(
    ConversationId ConversationId);
