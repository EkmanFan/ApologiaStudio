namespace ApologiaStudio.Domain.Conversations;

public readonly record struct MessageId(Guid Value)
{
    public static MessageId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}