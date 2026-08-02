using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Agents;

public sealed record ConversationMessageContext(
    MessageId MessageId,
    MessageRole Role,
    string Content,
    AgentId? AgentId,
    DateTimeOffset CreatedAt);
