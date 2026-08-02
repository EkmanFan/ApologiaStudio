using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Domain.Conversations;

public sealed class Conversation
{
    private readonly List<ConversationMessage> _messages = [];

    private Conversation(
        ConversationId id,
        UserId ownerId,
        string title,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerId = ownerId;
        Title = title;
        CreatedAt = createdAt;
    }

    public ConversationId Id { get; }

    public UserId OwnerId { get; }

    public string Title { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<ConversationMessage> Messages => _messages;

    public static Conversation Create(
        UserId ownerId,
        string title,
        DateTimeOffset createdAt)
    {
        if (ownerId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Owner identifier cannot be empty.",
                nameof(ownerId));
        }

        ValidateTitle(title);

        return new Conversation(
            ConversationId.New(),
            ownerId,
            title.Trim(),
            createdAt);
    }

    public ConversationMessage AddUserMessage(
        string content,
        DateTimeOffset createdAt)
    {
        var message = ConversationMessage.FromUser(content, createdAt);

        _messages.Add(message);

        return message;
    }

    public ConversationMessage AddAgentMessage(
        AgentId agentId,
        string content,
        DateTimeOffset createdAt)
    {
        var message = ConversationMessage.FromAgent(
            agentId,
            content,
            createdAt);

        _messages.Add(message);

        return message;
    }

    public void Rename(string title)
    {
        ValidateTitle(title);
        Title = title.Trim();
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Conversation title cannot be empty.",
                nameof(title));
        }

        if (title.Length > 200)
        {
            throw new ArgumentException(
                "Conversation title cannot exceed 200 characters.",
                nameof(title));
        }
    }
}