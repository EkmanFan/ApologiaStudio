using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class SimulatedAgentResponseProvider
{
    public string CreateResponse(
        AgentId agentId,
        string userMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        if (agentId == BuiltInAgents.Historian.Id)
        {
            return
                "Historian simulation: I would examine the chronology, " +
                "primary sources and historical development relevant to: " +
                $"\"{userMessage}\"";
        }

        if (agentId == BuiltInAgents.ProtestantApologist.Id)
        {
            return
                "Protestant apologist simulation: I would clarify the claim, " +
                "examine the biblical basis and construct a reasoned defence concerning: " +
                $"\"{userMessage}\"";
        }

        throw new ArgumentException(
            $"Agent '{agentId}' is not supported by the simulated runtime.",
            nameof(agentId));
    }
}
