namespace ApologiaStudio.AgentRuntime.Agents;

public interface IAgentRoutingSnapshotProvider
{
    ValueTask<IAgentRegistry> GetActiveAsync(
        CancellationToken cancellationToken);
}
