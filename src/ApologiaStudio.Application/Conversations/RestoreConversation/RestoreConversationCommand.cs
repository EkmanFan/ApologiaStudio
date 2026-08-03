using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Conversations.RestoreConversation;

public sealed record RestoreConversationCommand(
    ConversationId ConversationId);
