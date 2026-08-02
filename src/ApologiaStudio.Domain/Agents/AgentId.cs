namespace ApologiaStudio.Domain.Agents;

public readonly record struct AgentId(Guid Value)
{
    public static AgentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}