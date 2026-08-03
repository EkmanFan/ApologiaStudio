using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Projects;
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

    public ConversationProjectId? ProjectId { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

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

    public void MoveToProject(
        ConversationProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.OwnerId != OwnerId)
        {
            throw new InvalidOperationException(
                "A conversation cannot be moved to another user's project.");
        }

        ProjectId = project.Id;
    }

    public void MoveToChats()
    {
        ProjectId = null;
    }

    public void Reorder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "Conversation sort order cannot be negative.");
        }

        SortOrder = sortOrder;
    }

    public void Delete(DateTimeOffset deletedAt)
    {
        if (deletedAt < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deletedAt),
                "A conversation cannot be deleted before it was created.");
        }

        DeletedAt ??= deletedAt;
    }

    public void Restore()
    {
        DeletedAt = null;
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
