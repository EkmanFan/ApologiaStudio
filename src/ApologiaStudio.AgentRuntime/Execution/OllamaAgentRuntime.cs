using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.AgentRuntime.Execution;

public sealed class OllamaAgentRuntime(
    IAgentRouter agentRouter,
    AgentPromptCatalog promptCatalog,
    IAiRuntimeSettingsStore settingsStore,
    IOllamaHttpClientFactory httpClientFactory,
    IOllamaRuntimeTelemetry telemetry,
    IAgentSettingsStore? agentSettingsStore = null)
    : IRoutedAgentRuntime
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

        var settings =
            await settingsStore.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "AI runtime settings have not been initialized.");

        var agentSettings = agentSettingsStore is null
            ? null
            : await agentSettingsStore.GetAsync(
                routingDecision.AgentId,
                cancellationToken);

        var model =
            agentSettings is null
                ? settings.ResolveAgentModel(
                    routingDecision.AgentId)
                : string.IsNullOrWhiteSpace(agentSettings.Model)
                    ? settings.DefaultAgentModel
                    : agentSettings.Model;

        ValidateConfiguration(
            settings,
            model);

        yield return new AgentSelectedEvent(
            routingDecision.AgentId,
            agentSettings?.DisplayName ?? routingDecision.AgentName,
            routingDecision.Reason);

        var promptDefinition =
            agentSettings is null
                ? promptCatalog.Get(
                    routingDecision.AgentId)
                : new AgentPromptDefinition(
                    "database",
                    agentSettings.SystemPrompt);

        var messages =
            BuildMessages(
                request,
                promptDefinition,
                routingDecision.AgentId,
                settings);

        telemetry.GenerationStarted(
            new OllamaGenerationStartedObservation(
                request.ConversationId,
                routingDecision.AgentId,
                model,
                messages.Count - 1,
                settings.MaximumOutputTokens));

        var requestBody = new
        {
            model,
            messages,
            stream = true,
            think = false,
            keep_alive = settings.KeepAlive,
            options = new
            {
                temperature = 0.2,
                num_predict =
                    settings.MaximumOutputTokens
            }
        };

        var baseAddress =
            AiRuntimeSettingsValidator.NormalizeBaseAddress(
                settings.BaseAddress);

        using var httpClient =
            httpClientFactory.Create(
                baseAddress,
                TimeSpan.FromSeconds(
                    settings.GenerationTimeoutSeconds));

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
        OllamaChatChunk? completionChunk = null;
        var repetitionGuard = new OllamaRepetitionGuard();

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

                if (repetitionGuard.TryDetect(
                        completeResponse,
                        out var repetition))
                {
                    telemetry.GenerationRejected(
                        new OllamaGenerationRejectedObservation(
                            request.ConversationId,
                            routingDecision.AgentId,
                            model,
                            completeResponse.Length,
                            repetition.PatternLength,
                            repetition.RepeatCount));

                    throw new OllamaRepetitionDetectedException(
                        completeResponse.Length,
                        repetition.PatternLength,
                        repetition.RepeatCount);
                }

                yield return new TextDeltaEvent(
                    delta);
            }

            if (chunk.Done)
            {
                completionChunk = chunk;
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

        if (completionChunk is null)
        {
            throw new InvalidOperationException(
                "Ollama did not return a final completion chunk.");
        }

        if (string.IsNullOrWhiteSpace(completedText))
        {
            throw new InvalidOperationException(
                "Ollama completed the request without returning content.");
        }

        telemetry.GenerationCompleted(
            new OllamaGenerationCompletedObservation(
                request.ConversationId,
                routingDecision.AgentId,
                model,
                string.IsNullOrWhiteSpace(
                    completionChunk.DoneReason)
                    ? "unknown"
                    : completionChunk.DoneReason,
                completionChunk.PromptEvaluationCount,
                completionChunk.EvaluationCount,
                completionChunk.TotalDuration,
                completionChunk.LoadDuration,
                completionChunk.PromptEvaluationDuration,
                completionChunk.EvaluationDuration));

        yield return new AgentTurnCompletedEvent(
            routingDecision.AgentId,
            completedText);
    }

    private IReadOnlyList<OllamaRequestMessage>
        BuildMessages(
            AgentTurnRequest request,
            AgentPromptDefinition promptDefinition,
            AgentId agentId,
            AiRuntimeSettingsSnapshot settings)
    {
        var currentMessageIndex =
            FindCurrentUserMessageIndex(request);

        var selectedHistory =
            new List<OllamaRequestMessage>();

        var characterCount = 0;

        for (var index = currentMessageIndex;
             index >= 0 &&
             selectedHistory.Count <
                 settings.MaximumHistoryMessages;
             index--)
        {
            var message =
                request.History[index];

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            if (message.Role == MessageRole.Agent &&
                OllamaRepetitionDetector.TryDetect(
                    message.Content,
                    out var repetition))
            {
                telemetry.HistoryMessageSkipped(
                    new OllamaHistoryMessageSkippedObservation(
                        request.ConversationId,
                        message.MessageId,
                        message.AgentId,
                        message.Content.Length,
                        repetition.PatternLength,
                        repetition.RepeatCount));

                continue;
            }

            var remainingCharacters =
                settings.MaximumHistoryCharacters -
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

    private static void ValidateConfiguration(
        AiRuntimeSettingsSnapshot settings,
        string model)
    {
        AiRuntimeSettingsValidator.NormalizeBaseAddress(
            settings.BaseAddress);

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                "The Ollama response model is not configured.");
        }

        if (settings.GenerationTimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException(
                "GenerationTimeoutSeconds must be between 1 and 600.");
        }

        if (settings.MaximumHistoryMessages <= 0)
        {
            throw new InvalidOperationException(
                "MaximumHistoryMessages must be positive.");
        }

        if (settings.MaximumHistoryCharacters <= 0)
        {
            throw new InvalidOperationException(
                "MaximumHistoryCharacters must be positive.");
        }

        if (settings.MaximumOutputTokens <= 0)
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

        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; init; }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; init; }

        [JsonPropertyName("load_duration")]
        public long? LoadDuration { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvaluationCount { get; init; }

        [JsonPropertyName("prompt_eval_duration")]
        public long? PromptEvaluationDuration { get; init; }

        [JsonPropertyName("eval_count")]
        public int? EvaluationCount { get; init; }

        [JsonPropertyName("eval_duration")]
        public long? EvaluationDuration { get; init; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } =
            string.Empty;
    }
}
