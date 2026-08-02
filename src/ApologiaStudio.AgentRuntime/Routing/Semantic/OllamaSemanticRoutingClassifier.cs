using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed class OllamaSemanticRoutingClassifier(
    HttpClient httpClient,
    OllamaRoutingOptions options)
    : ISemanticRoutingClassifier,
      IDisposable
{
    private const int MaximumErrorBodyLength = 2_000;

    private const string SystemPrompt = """
        You are a routing classifier for a Christian apologetics
        application.

        Select exactly one specialist.

        historian:
        - historical people, rulers, events and institutions;
        - chronology, dates, durations and ages at historical events;
        - councils, political history and Church history;
        - development of doctrines or practices through history;
        - descriptive questions about what happened historically.

        protestant-apologist:
        - defence of Christian or Protestant beliefs;
        - biblical doctrine and theological interpretation;
        - objections from atheism, Islam, Catholicism or Orthodoxy;
        - arguments for God, Christ, resurrection or Scripture;
        - normative questions about what Christians should believe.

        Important distinctions:
        - A religious subject can still be primarily historical.
        - Dates, ages, reigns and chronology belong to the historian.
        - Defence, refutation and doctrinal justification belong to
          the Protestant apologist.
        - Do not answer the user's question.
        - Return only JSON matching the supplied schema.
        - The agent value must be exactly historian or
          protestant-apologist.
        - Confidence must be between 0.0 and 1.0.
        - Write the reason in French.
        """;

    private static readonly JsonElement RoutingSchema =
        JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "type": "object",
              "properties": {
                "agent": {
                  "type": "string",
                  "enum": [
                    "historian",
                    "protestant-apologist"
                  ]
                },
                "confidence": {
                  "type": "number"
                },
                "reason": {
                  "type": "string"
                }
              },
              "required": [
                "agent",
                "confidence",
                "reason"
              ],
              "additionalProperties": false
            }
            """);

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async ValueTask<SemanticRoutingResult> ClassifyAsync(
        string userMessage,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            userMessage);

        ValidateConfiguration();

        var request = new
        {
            model = options.Model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = SystemPrompt
                },
                new
                {
                    role = "user",
                    content = userMessage
                }
            },
            stream = false,
            think = false,
            format = RoutingSchema,
            options = new
            {
                temperature = 0,
                num_predict = 160
            },
            keep_alive = options.KeepAlive
        };

        using var response =
            await httpClient.PostAsJsonAsync(
                "api/chat",
                request,
                JsonOptions,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException(
                response.StatusCode,
                responseBody);
        }

        var ollamaResponse =
            JsonSerializer.Deserialize<OllamaChatResponse>(
                responseBody,
                JsonOptions)
            ?? throw new InvalidOperationException(
                "Ollama returned an invalid response document.");

        var json =
            ollamaResponse.Message?.Content;

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                "Ollama returned an empty routing classification.");
        }

        var payload =
            JsonSerializer.Deserialize<RoutingPayload>(
                json,
                JsonOptions)
            ?? throw new InvalidOperationException(
                "Ollama returned an invalid routing payload.");

        ValidatePayload(payload);

        return new SemanticRoutingResult(
            payload.Agent,
            payload.Confidence,
            payload.Reason);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    private void ValidateConfiguration()
    {
        if (!options.BaseAddress.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                "The Ollama base address must be absolute.");
        }

        if (!options.BaseAddress.IsLoopback)
        {
            throw new InvalidOperationException(
                "The local Ollama endpoint must use a loopback address.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException(
                "The Ollama routing model is not configured.");
        }
    }

    private static void ValidatePayload(
        RoutingPayload payload)
    {
        if (payload.Agent is not
            ("historian" or "protestant-apologist"))
        {
            throw new InvalidOperationException(
                $"Ollama returned an unknown agent: '{payload.Agent}'.");
        }

        if (!double.IsFinite(payload.Confidence) ||
            payload.Confidence is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Ollama returned an invalid confidence score.");
        }

        if (string.IsNullOrWhiteSpace(payload.Reason))
        {
            throw new InvalidOperationException(
                "Ollama returned an empty routing reason.");
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

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; init; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } =
            string.Empty;
    }

    private sealed class RoutingPayload
    {
        [JsonPropertyName("agent")]
        public string Agent { get; init; } =
            string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }

        [JsonPropertyName("reason")]
        public string Reason { get; init; } =
            string.Empty;
    }
}
