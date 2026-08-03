using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Domain.Projects;

public sealed class ConversationProject
{
    private ConversationProject(
        ConversationProjectId id,
        UserId ownerId,
        string name,
        DateTimeOffset createdAt,
        int sortOrder)
    {
        Id = id;
        OwnerId = ownerId;
        Name = name;
        CreatedAt = createdAt;
        SortOrder = sortOrder;
    }

    public ConversationProjectId Id { get; }

    public UserId OwnerId { get; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public int SortOrder { get; private set; }

    public static ConversationProject Create(
        UserId ownerId,
        string name,
        DateTimeOffset createdAt,
        int sortOrder = 0)
    {
        if (ownerId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Owner identifier cannot be empty.",
                nameof(ownerId));
        }

        ValidateName(name);
        ValidateSortOrder(sortOrder);

        return new ConversationProject(
            ConversationProjectId.New(),
            ownerId,
            name.Trim(),
            createdAt,
            sortOrder);
    }

    public void Rename(string name)
    {
        ValidateName(name);
        Name = name.Trim();
    }

    public void Reorder(int sortOrder)
    {
        ValidateSortOrder(sortOrder);
        SortOrder = sortOrder;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Project name cannot be empty.",
                nameof(name));
        }

        if (name.Length > 120)
        {
            throw new ArgumentException(
                "Project name cannot exceed 120 characters.",
                nameof(name));
        }
    }

    private static void ValidateSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "Project sort order cannot be negative.");
        }
    }
}
