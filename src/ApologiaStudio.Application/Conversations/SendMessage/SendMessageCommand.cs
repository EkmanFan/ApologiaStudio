using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Conversations.SendMessage;

public sealed record SendMessageCommand(
    ConversationId ConversationId,
    string Content,
    AgentId? RequestedAgentId = null);
