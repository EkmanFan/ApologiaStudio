using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApologiaStudio.KnowledgeImporter;

internal sealed class OllamaListwiseRerankerClient : IDisposable
{
    private const int MaximumErrorBodyLength = 2_000;
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public OllamaListwiseRerankerClient(Uri baseAddress, TimeSpan timeout)
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
        using var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
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

    public async Task<OllamaListwiseRerankResult> RerankAsync(
        string model,
        string question,
        IReadOnlyList<RerankerCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            throw new KnowledgeImportException(
                "Listwise reranking requires at least one candidate segment.");
        }

        var ids = candidates.Select(candidate => candidate.CandidateId).ToArray();
        var candidatePayload = candidates.Select(candidate => new
        {
            id = candidate.CandidateId,
            text = candidate.Evidence.ChunkText
        });
        var userPrompt =
            $"QUESTION:\n{question.Trim()}\n\n" +
            "CANDIDATES_JSON:\n" +
            JsonSerializer.Serialize(candidatePayload, JsonOptions);
        var requestBody = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = DeDecretisRerankerProfile.SystemPrompt
                },
                new
                {
                    role = "user",
                    content = userPrompt
                }
            },
            stream = false,
            think = false,
            format = CreateResponseSchema(ids),
            keep_alive = DeDecretisRerankerProfile.KeepAlive,
            options = new
            {
                temperature = 0,
                seed = 0,
                num_predict = DeDecretisRerankerProfile.MaximumOutputTokens
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/chat",
            requestBody,
            JsonOptions,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (errorBody.Length > MaximumErrorBodyLength)
            {
                errorBody = errorBody[..MaximumErrorBodyLength];
            }

            throw new KnowledgeImportException(
                $"Ollama listwise reranking returned HTTP {(int)response.StatusCode} " +
                $"({response.StatusCode}). Response: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new KnowledgeImportException(
                "Ollama returned an empty listwise-reranking response.");
        if (!payload.Done)
        {
            throw new KnowledgeImportException(
                "Ollama listwise reranking did not report completion.");
        }

        if (string.Equals(payload.DoneReason, "length", StringComparison.OrdinalIgnoreCase))
        {
            throw new KnowledgeImportException(
                "Ollama listwise reranking reached the output-token limit before completion.");
        }

        var content = payload.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new KnowledgeImportException(
                "Ollama listwise reranking completed without structured content.");
        }

        var modelResponse = JsonSerializer.Deserialize<ListwiseRankingModelResponse>(
            content,
            JsonOptions)
            ?? throw new KnowledgeImportException(
                "Ollama listwise reranking returned an empty structured payload.");
        var orderedIds = modelResponse.OrderedIds
            ?? throw new KnowledgeImportException(
                "Ollama listwise reranking returned no ordered candidate ids.");
        ValidateOrdering(ids, orderedIds);

        return new OllamaListwiseRerankResult(
            orderedIds,
            payload.PromptEvaluationCount,
            payload.EvaluationCount,
            payload.TotalDuration,
            payload.LoadDuration);
    }

    public void Dispose() => _httpClient.Dispose();

    private static JsonElement CreateResponseSchema(IReadOnlyList<string> candidateIds)
    {
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                orderedIds = new
                {
                    type = "array",
                    minItems = candidateIds.Count,
                    maxItems = candidateIds.Count,
                    uniqueItems = true,
                    items = new
                    {
                        type = "string",
                        @enum = candidateIds
                    }
                }
            },
            required = new[] { "orderedIds" }
        };

        return JsonSerializer.SerializeToElement(schema, JsonOptions);
    }

    private static void ValidateOrdering(
        IReadOnlyCollection<string> expectedIds,
        IReadOnlyCollection<string> orderedIds)
    {
        if (expectedIds.Count != orderedIds.Count ||
            orderedIds.Count != orderedIds.Distinct(StringComparer.Ordinal).Count())
        {
            throw new KnowledgeImportException(
                "Ollama listwise reranking returned an incomplete or duplicate ordering.");
        }

        var expected = expectedIds.ToHashSet(StringComparer.Ordinal);
        if (orderedIds.Any(id => !expected.Contains(id)))
        {
            throw new KnowledgeImportException(
                "Ollama listwise reranking returned an unknown candidate id.");
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record OllamaTagsResponse(OllamaModel[]? Models);

    private sealed record OllamaModel(string? Name, string? Model, string? Digest);

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
