using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Conversations.RenameConversation;

public sealed record RenameConversationCommand(
    ConversationId ConversationId,
    string Title);
