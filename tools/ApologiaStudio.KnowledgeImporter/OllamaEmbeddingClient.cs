using System.Net.Http.Json;
using System.Text.Json;

namespace ApologiaStudio.KnowledgeImporter;

internal sealed class OllamaEmbeddingClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public OllamaEmbeddingClient(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        _httpClient = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = Timeout.InfiniteTimeSpan
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
            x => string.Equals(x.Name, model, StringComparison.Ordinal) ||
                 string.Equals(x.Model, model, StringComparison.Ordinal));

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

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        string model,
        int dimensions,
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0 || inputs.Any(string.IsNullOrWhiteSpace))
        {
            throw new KnowledgeImportException(
                "Embedding inputs must contain at least one non-empty text.");
        }

        var request = new OllamaEmbedRequest(
            model,
            inputs,
            false,
            dimensions);

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/embed",
            request,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(
            JsonOptions,
            cancellationToken);
        var embeddings = payload?.Embeddings;

        if (embeddings is null || embeddings.Length != inputs.Count)
        {
            throw new KnowledgeImportException(
                $"Ollama returned {embeddings?.Length ?? 0} embeddings for {inputs.Count} inputs.");
        }

        for (var index = 0; index < embeddings.Length; index++)
        {
            var embedding = embeddings[index];
            if (embedding is null || embedding.Length != dimensions)
            {
                throw new KnowledgeImportException(
                    $"Embedding {index} has {embedding?.Length ?? 0} dimensions; expected {dimensions}.");
            }

            if (embedding.Any(value => !float.IsFinite(value)))
            {
                throw new KnowledgeImportException(
                    $"Embedding {index} contains a non-finite value.");
            }

            if (embedding.All(value => value == 0f))
            {
                throw new KnowledgeImportException(
                    $"Embedding {index} is the zero vector.");
            }
        }

        return embeddings;
    }

    public void Dispose() => _httpClient.Dispose();

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record OllamaTagsResponse(OllamaModel[]? Models);

    private sealed record OllamaModel(
        string? Name,
        string? Model,
        string? Digest);

    private sealed record OllamaEmbedRequest(
        string Model,
        IReadOnlyList<string> Input,
        bool Truncate,
        int Dimensions);

    private sealed record OllamaEmbedResponse(float[][]? Embeddings);
}
