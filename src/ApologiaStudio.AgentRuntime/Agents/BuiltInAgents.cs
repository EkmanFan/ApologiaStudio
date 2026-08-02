using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public static class BuiltInAgents
{
    public static readonly AgentDescriptor Historian = new(
        new AgentId(
            Guid.Parse("11111111-1111-1111-1111-111111111111")),
        "historian",
        "Historian of Religions");

    public static readonly AgentDescriptor ProtestantApologist = new(
        new AgentId(
            Guid.Parse("22222222-2222-2222-2222-222222222222")),
        "protestant-apologist",
        "Protestant Apologist");

    public static IReadOnlyCollection<AgentDescriptor> All { get; } =
    [
        Historian,
        ProtestantApologist
    ];

    public static bool TryGet(
        AgentId agentId,
        out AgentDescriptor descriptor)
    {
        var result = All.FirstOrDefault(
            candidate => candidate.Id == agentId);

        if (result is null)
        {
            descriptor = null!;
            return false;
        }

        descriptor = result;
        return true;
    }
}
