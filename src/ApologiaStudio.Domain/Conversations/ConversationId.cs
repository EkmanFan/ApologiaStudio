namespace ApologiaStudio.Domain.Conversations;

public readonly record struct ConversationId(Guid Value)
{
    public static ConversationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}