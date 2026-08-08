using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed class OllamaModelCatalogClient(
    HttpClient httpClient)
    : IOllamaModelCatalogClient
{
    public async Task<IReadOnlyList<OllamaLocalModel>>
        ListLocalModelsAsync(
            Uri baseAddress,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        var normalizedBaseAddress =
            OllamaRoutingSettingsValidator
                .NormalizeBaseAddress(
                    baseAddress.ToString());

        var endpoint =
            new Uri(
                normalizedBaseAddress,
                "api/tags");

        using var response =
            await httpClient.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        OllamaTagsResponse? payload;

        try
        {
            payload =
                await response.Content.ReadFromJsonAsync<
                    OllamaTagsResponse>(
                    cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Ollama returned an invalid local-model catalog.",
                exception);
        }

        return (payload?.Models ??
                Array.Empty<OllamaModelResponse>())
            .Where(
                model =>
                    !string.IsNullOrWhiteSpace(model.Name))
            .GroupBy(
                model => model.Name.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(
                model =>
                    new OllamaLocalModel(
                        model.Name.Trim(),
                        model.Details?.Family,
                        model.Details?.ParameterSize,
                        model.Details?.QuantizationLevel))
            .OrderBy(
                model => model.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record OllamaTagsResponse(
        IReadOnlyList<OllamaModelResponse>? Models);

    private sealed record OllamaModelResponse(
        string Name,
        OllamaModelDetailsResponse? Details);

    private sealed record OllamaModelDetailsResponse(
        string? Family,
        [property: JsonPropertyName("parameter_size")]
        string? ParameterSize,
        [property: JsonPropertyName("quantization_level")]
        string? QuantizationLevel);
}
