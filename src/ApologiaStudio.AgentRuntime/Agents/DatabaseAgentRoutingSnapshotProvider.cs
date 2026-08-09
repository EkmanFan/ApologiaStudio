using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents;

namespace ApologiaStudio.AgentRuntime.Agents;

public sealed class DatabaseAgentRoutingSnapshotProvider(
    IAgentSettingsStore settingsStore)
    : IAgentRoutingSnapshotProvider
{
    public async ValueTask<IAgentRegistry> GetActiveAsync(
        CancellationToken cancellationToken)
    {
        var settings = await settingsStore
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registry = new AgentRegistry(
            settings
                .Where(candidate => candidate.IsEnabled)
                .Select(AgentRoutingProfile.FromSettings));

        foreach (var builtInAgent in BuiltInAgents.All)
        {
            if (!registry.TryGet(builtInAgent.Id, out _))
            {
                throw new InvalidOperationException(
                    $"Required built-in agent '{builtInAgent.Slug}' " +
                    "is missing from the active agent settings.");
            }
        }

        return registry;
    }
}
