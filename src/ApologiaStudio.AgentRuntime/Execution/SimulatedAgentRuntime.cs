using System.Runtime.CompilerServices;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class SimulatedAgentRuntime(
    IAgentRouter agentRouter,
    SimulatedAgentResponseProvider responseProvider)
    : IAgentRuntime
{
    public async IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
        AgentTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var routingDecision = agentRouter.Route(request);

        yield return new AgentSelectedEvent(
            routingDecision.AgentId,
            routingDecision.AgentName,
            routingDecision.Reason);

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var userMessage = FindCurrentUserMessage(request);

        var completeResponse = responseProvider.CreateResponse(
            routingDecision.AgentId,
            userMessage);

        foreach (var chunk in SplitIntoChunks(
                     completeResponse,
                     maximumChunkLength: 48))
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return new TextDeltaEvent(chunk);

            await Task.Yield();
        }

        yield return new AgentTurnCompletedEvent(
            routingDecision.AgentId,
            completeResponse);
    }

    private static string FindCurrentUserMessage(
        AgentTurnRequest request)
    {
        var currentMessage = request.History.FirstOrDefault(
            message =>
                message.MessageId == request.UserMessageId &&
                message.Role == MessageRole.User);

        if (currentMessage is null)
        {
            throw new InvalidOperationException(
                "The current user message was not found in the conversation history.");
        }

        return currentMessage.Content;
    }

    private static IEnumerable<string> SplitIntoChunks(
        string content,
        int maximumChunkLength)
    {
        if (maximumChunkLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumChunkLength));
        }

        for (var position = 0;
             position < content.Length;
             position += maximumChunkLength)
        {
            var length = Math.Min(
                maximumChunkLength,
                content.Length - position);

            yield return content.Substring(
                position,
                length);
        }
    }
}
