using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.Domain.Conversations;

public sealed class ConversationMessage
{
    private ConversationMessage(
        MessageId id,
        MessageRole role,
        string content,
        DateTimeOffset createdAt,
        AgentId? agentId)
    {
        Id = id;
        Role = role;
        Content = content;
        CreatedAt = createdAt;
        AgentId = agentId;
    }

    public MessageId Id { get; }

    public MessageRole Role { get; }

    public string Content { get; }

    public DateTimeOffset CreatedAt { get; }

    public AgentId? AgentId { get; }

    public static ConversationMessage FromUser(
        string content,
        DateTimeOffset createdAt)
    {
        ValidateContent(content);

        return new ConversationMessage(
            MessageId.New(),
            MessageRole.User,
            content.Trim(),
            createdAt,
            agentId: null);
    }

    public static ConversationMessage FromAgent(
        AgentId agentId,
        string content,
        DateTimeOffset createdAt)
    {
        if (agentId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Agent identifier cannot be empty.",
                nameof(agentId));
        }

        ValidateContent(content);

        return new ConversationMessage(
            MessageId.New(),
            MessageRole.Agent,
            content.Trim(),
            createdAt,
            agentId);
    }

    private static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "Message content cannot be empty.",
                nameof(content));
        }

        if (content.Length > 50_000)
        {
            throw new ArgumentException(
                "Message content cannot exceed 50,000 characters.",
                nameof(content));
        }
    }
}