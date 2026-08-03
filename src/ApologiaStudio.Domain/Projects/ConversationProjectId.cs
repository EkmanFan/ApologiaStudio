namespace ApologiaStudio.Domain.Projects;

public readonly record struct ConversationProjectId(Guid Value)
{
    public static ConversationProjectId New() =>
        new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
