using ApologiaStudio.Application.Agents;

namespace ApologiaStudio.Application.Abstractions.Agents;

public interface IAgentRuntime
{
    IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken);
}
