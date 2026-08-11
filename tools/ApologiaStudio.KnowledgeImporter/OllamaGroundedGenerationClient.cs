using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApologiaStudio.KnowledgeImporter;

internal sealed class OllamaGroundedGenerationClient : IDisposable
{
    private const int MaximumErrorBodyLength = 2_000;
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public OllamaGroundedGenerationClient(
        Uri baseAddress,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _httpClient = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = timeout
        };
    }

    public async Task<string> ResolveModelDigestAsync(
        string model,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        using var response = await _httpClient.GetAsync(
            "/api/tags",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
            JsonOptions,
            cancellationToken);
        var match = payload?.Models?.SingleOrDefault(
            candidate =>
                string.Equals(candidate.Name, model, StringComparison.Ordinal) ||
                string.Equals(candidate.Model, model, StringComparison.Ordinal));
        if (match is null)
        {
            throw new KnowledgeImportException(
                $"Ollama model '{model}' is not installed locally.");
        }

        var digest = match.Digest?.Trim().ToLowerInvariant();
        if (!IsSha256(digest))
        {
            throw new KnowledgeImportException(
                $"Ollama returned an invalid digest for model '{model}'.");
        }

        return digest!;
    }

    public async Task<OllamaGroundedGenerationResult> GenerateAsync(
        string model,
        string question,
        IReadOnlyList<GroundedEvidence> evidence,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count == 0)
        {
            throw new KnowledgeImportException(
                "Grounded generation requires at least one evidence segment.");
        }

        var evidenceIds = evidence
            .Select(item => item.EvidenceId)
            .ToArray();
        var format = CreateResponseSchema(evidenceIds);
        var evidencePayload = evidence.Select(item => new
        {
            id = item.EvidenceId,
            work = item.WorkTitle,
            text = item.Text
        });
        var serializedEvidence = JsonSerializer.Serialize(
            evidencePayload,
            JsonOptions);
        var userPrompt =
            $"QUESTION:\n{question.Trim()}\n\n" +
            "EVIDENCE_JSON:\n" + serializedEvidence;

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = DeDecretisGroundedAnswerProfile.SystemPrompt
                },
                new
                {
                    role = "user",
                    content = userPrompt
                }
            },
            stream = false,
            think = false,
            format,
            keep_alive = DeDecretisGroundedAnswerProfile.KeepAlive,
            options = new
            {
                temperature = 0,
                num_predict = DeDecretisGroundedAnswerProfile.MaximumOutputTokens
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/chat",
            requestBody,
            JsonOptions,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(
                cancellationToken);
            if (errorBody.Length > MaximumErrorBodyLength)
            {
                errorBody = errorBody[..MaximumErrorBodyLength];
            }

            throw new KnowledgeImportException(
                $"Ollama grounded generation returned HTTP {(int)response.StatusCode} " +
                $"({response.StatusCode}). Response: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new KnowledgeImportException(
                "Ollama returned an empty grounded-generation response.");

        if (!payload.Done)
        {
            throw new KnowledgeImportException(
                "Ollama grounded generation did not report completion.");
        }

        if (string.Equals(payload.DoneReason, "length", StringComparison.OrdinalIgnoreCase))
        {
            throw new KnowledgeImportException(
                "Ollama grounded generation reached the output-token limit before completion.");
        }

        var content = payload.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new KnowledgeImportException(
                "Ollama grounded generation completed without structured content.");
        }

        var modelResponse = JsonSerializer.Deserialize<GroundedAnswerModelResponse>(
            content,
            JsonOptions)
            ?? throw new KnowledgeImportException(
                "Ollama grounded generation returned an empty structured payload.");

        return new OllamaGroundedGenerationResult(
            modelResponse,
            payload.PromptEvaluationCount,
            payload.EvaluationCount,
            payload.TotalDuration,
            payload.LoadDuration);
    }

    public void Dispose() => _httpClient.Dispose();

    private static JsonElement CreateResponseSchema(
        IReadOnlyList<string> evidenceIds)
    {
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                status = new
                {
                    type = "string",
                    @enum = new[] { "answered", "insufficient_evidence" }
                },
                claims = new
                {
                    type = "array",
                    maxItems = DeDecretisGroundedAnswerProfile.MaximumClaims,
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            text = new
                            {
                                type = "string",
                                minLength = 1,
                                maxLength = DeDecretisGroundedAnswerProfile.MaximumClaimCharacters
                            },
                            evidenceIds = new
                            {
                                type = "array",
                                minItems = 1,
                                maxItems = DeDecretisGroundedAnswerProfile.MaximumEvidenceIdsPerClaim,
                                uniqueItems = true,
                                items = new
                                {
                                    type = "string",
                                    @enum = evidenceIds
                                }
                            }
                        },
                        required = new[] { "text", "evidenceIds" }
                    }
                }
            },
            required = new[] { "status", "claims" }
        };

        return JsonSerializer.SerializeToElement(schema, JsonOptions);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record OllamaTagsResponse(OllamaModel[]? Models);

    private sealed record OllamaModel(
        string? Name,
        string? Model,
        string? Digest);

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; init; }

        [JsonPropertyName("done")]
        public bool Done { get; init; }

        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; init; }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; init; }

        [JsonPropertyName("load_duration")]
        public long? LoadDuration { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvaluationCount { get; init; }

        [JsonPropertyName("eval_count")]
        public int? EvaluationCount { get; init; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }
}
