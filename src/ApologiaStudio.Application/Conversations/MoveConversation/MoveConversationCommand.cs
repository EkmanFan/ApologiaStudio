using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Conversations.MoveConversation;

public sealed record MoveConversationCommand(
    ConversationId ConversationId,
    ConversationProjectId? ProjectId,
    int Position);
