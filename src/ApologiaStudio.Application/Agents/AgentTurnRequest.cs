using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Agents;

public sealed record AgentTurnRequest(
    ConversationId ConversationId,
    UserId UserId,
    MessageId UserMessageId,
    AgentId? RequestedAgentId,
    IReadOnlyList<ConversationMessageContext> History,
    ApplicationLanguage TheologicalLanguage =
        ApplicationLanguage.French);
