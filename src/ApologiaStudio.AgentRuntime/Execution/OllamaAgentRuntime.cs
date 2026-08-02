using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class OllamaAgentRuntime(
    IAgentRouter agentRouter,
    AgentPromptCatalog promptCatalog,
    HttpClient httpClient,
    OllamaGenerationOptions options)
    : IRoutedAgentRuntime,
      IDisposable
{
    private const int MaximumErrorBodyLength = 2_000;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
        AgentTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var routingDecision =
            await agentRouter.RouteAsync(
                request,
                cancellationToken);

        await foreach (var runEvent in RunTurnAsync(
                           request,
                           routingDecision,
                           cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            yield return runEvent;
        }
    }

    public async IAsyncEnumerable<AgentRunEvent> RunTurnAsync(
        AgentTurnRequest request,
        RoutingDecision routingDecision,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(routingDecision);

        ValidateConfiguration();

        yield return new AgentSelectedEvent(
            routingDecision.AgentId,
            routingDecision.AgentName,
            routingDecision.Reason);

        var promptDefinition =
            promptCatalog.Get(
                routingDecision.AgentId);

        var messages =
            BuildMessages(
                request,
                promptDefinition,
                routingDecision.AgentId);

        var requestBody = new
        {
            model = options.Model,
            messages,
            stream = true,
            think = false,
            keep_alive = options.KeepAlive,
            options = new
            {
                temperature = 0.2,
                num_predict =
                    options.MaximumOutputTokens
            }
        };

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                "api/chat")
            {
                Content =
                    JsonContent.Create(
                        requestBody,
                        options: JsonOptions)
            };

        using var response =
            await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw CreateHttpException(
                response.StatusCode,
                errorBody);
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var reader =
            new StreamReader(responseStream);

        var completeResponse =
            new StringBuilder();

        var streamCompleted = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line =
                await reader.ReadLineAsync(
                    cancellationToken);

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk =
                JsonSerializer.Deserialize<OllamaChatChunk>(
                    line,
                    JsonOptions)
                ?? throw new InvalidOperationException(
                    "Ollama returned an invalid streaming chunk.");

            if (!string.IsNullOrWhiteSpace(chunk.Error))
            {
                throw new InvalidOperationException(
                    $"Ollama generation failed: {chunk.Error}");
            }

            var delta =
                chunk.Message?.Content;

            if (!string.IsNullOrEmpty(delta))
            {
                completeResponse.Append(delta);

                yield return new TextDeltaEvent(
                    delta);
            }

            if (chunk.Done)
            {
                streamCompleted = true;
                break;
            }
        }

        if (!streamCompleted)
        {
            throw new InvalidOperationException(
                "The Ollama response stream ended before completion.");
        }

        var completedText =
            completeResponse.ToString();

        if (string.IsNullOrWhiteSpace(completedText))
        {
            throw new InvalidOperationException(
                "Ollama completed the request without returning content.");
        }

        yield return new AgentTurnCompletedEvent(
            routingDecision.AgentId,
            completedText);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    private IReadOnlyList<OllamaRequestMessage>
        BuildMessages(
            AgentTurnRequest request,
            AgentPromptDefinition promptDefinition,
            AgentId agentId)
    {
        var currentMessageIndex =
            FindCurrentUserMessageIndex(request);

        var selectedHistory =
            new List<OllamaRequestMessage>();

        var characterCount = 0;

        for (var index = currentMessageIndex;
             index >= 0 &&
             selectedHistory.Count <
                 options.MaximumHistoryMessages;
             index--)
        {
            var message =
                request.History[index];

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            var remainingCharacters =
                options.MaximumHistoryCharacters -
                characterCount;

            if (remainingCharacters <= 0)
            {
                break;
            }

            var content =
                message.Content;

            if (content.Length > remainingCharacters)
            {
                if (selectedHistory.Count > 0)
                {
                    break;
                }

                content =
                    content[..remainingCharacters];
            }

            selectedHistory.Add(
                new OllamaRequestMessage(
                    Role:
                        message.Role ==
                            MessageRole.User
                            ? "user"
                            : "assistant",
                    Content:
                        content));

            characterCount +=
                content.Length;
        }

        selectedHistory.Reverse();

        var messages =
            new List<OllamaRequestMessage>(
                selectedHistory.Count + 1)
            {
                new(
                    Role: "system",
                    Content:
                        CreateSystemPrompt(
                            promptDefinition,
                            agentId,
                            request.TheologicalLanguage))
            };

        messages.AddRange(
            selectedHistory);

        return messages;
    }

    private static string CreateSystemPrompt(
        AgentPromptDefinition promptDefinition,
        AgentId agentId,
        ApplicationLanguage theologicalLanguage)
    {
        if (agentId != BuiltInAgents.ProtestantApologist.Id)
        {
            return promptDefinition.SystemPrompt;
        }

        var languageName = theologicalLanguage ==
                ApplicationLanguage.English
            ? "English"
            : "French";

        return promptDefinition.SystemPrompt +
               "\n\nUser preference:\n" +
               $"- default to {languageName} for theological responses;\n" +
               "- if the latest user message explicitly requests French " +
               "or English for this response, honor that explicit request.";
    }

    private static int FindCurrentUserMessageIndex(
        AgentTurnRequest request)
    {
        for (var index =
                 request.History.Count - 1;
             index >= 0;
             index--)
        {
            var message =
                request.History[index];

            if (message.MessageId ==
                    request.UserMessageId &&
                message.Role ==
                    MessageRole.User)
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            "The current user message was not found in the conversation history.");
    }

    private void ValidateConfiguration()
    {
        if (!options.BaseAddress.IsAbsoluteUri ||
            !options.BaseAddress.IsLoopback)
        {
            throw new InvalidOperationException(
                "Ollama must use an absolute loopback address.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException(
                "The Ollama response model is not configured.");
        }

        if (options.MaximumHistoryMessages <= 0)
        {
            throw new InvalidOperationException(
                "MaximumHistoryMessages must be positive.");
        }

        if (options.MaximumHistoryCharacters <= 0)
        {
            throw new InvalidOperationException(
                "MaximumHistoryCharacters must be positive.");
        }

        if (options.MaximumOutputTokens <= 0)
        {
            throw new InvalidOperationException(
                "MaximumOutputTokens must be positive.");
        }
    }

    private static Exception CreateHttpException(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var safeBody =
            responseBody.Length <= MaximumErrorBodyLength
                ? responseBody
                : responseBody[..MaximumErrorBodyLength];

        return new HttpRequestException(
            $"Ollama returned HTTP {(int)statusCode} " +
            $"({statusCode}). Response: {safeBody}",
            inner: null,
            statusCode);
    }

    private sealed record OllamaRequestMessage(
        string Role,
        string Content);

    private sealed class OllamaChatChunk
    {
        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; init; }

        [JsonPropertyName("done")]
        public bool Done { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } =
            string.Empty;
    }
}
