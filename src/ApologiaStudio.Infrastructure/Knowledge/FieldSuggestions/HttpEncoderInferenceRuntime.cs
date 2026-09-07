using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

/// <summary>
/// Where the resident encoder worker listens.
/// </summary>
/// <remarks>
/// Apologia knows an endpoint and nothing else: not Docker, not a container
/// lifecycle, not a model path. Starting and stopping the worker is an
/// operational concern outside the application.
/// </remarks>
public sealed record EncoderInferenceOptions(
    Uri? BaseAddress,
    TimeSpan Timeout)
{
    public static EncoderInferenceOptions Disabled { get; } =
        new(null, TimeSpan.FromSeconds(30));

    public bool IsConfigured => BaseAddress is not null;
}

/// <summary>
/// Talks to the resident CPU encoder worker over local HTTP.
/// </summary>
/// <remarks>
/// A worker that is absent, unreachable or not yet loaded is
/// <see cref="EncoderInferenceUnavailableException"/>; a worker that answers
/// badly is <see cref="EncoderInferenceException"/>. Collapsing the two would
/// let a real defect be reported to a reviewer as "no assistance configured".
/// </remarks>
public sealed class HttpEncoderInferenceRuntime(
    HttpClient httpClient,
    EncoderInferenceOptions options)
    : IEncoderInferenceRuntime
{
    #region Methods

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(
        CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            return false;
        }

        try
        {
            using var response = await httpClient.GetAsync(
                new Uri(options.BaseAddress!, "health"),
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            // The worker did not answer in time: it is not usable right now.
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<EncoderInferenceResult> InferAsync(
        EncoderInferenceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!options.IsConfigured)
        {
            throw new EncoderInferenceUnavailableException(
                "No encoder runtime endpoint is configured.");
        }

        HttpResponseMessage response;

        try
        {
            // Buffered rather than streamed, so the request carries a
            // Content-Length. A chunked body would reach the worker as an
            // empty one, and a malformed request would be indistinguishable
            // from a genuine model failure.
            using var content = new StringContent(
                JsonSerializer.Serialize(
                    new InferPayload(request.ModelId, request.SerializedInput)),
                Encoding.UTF8,
                "application/json");

            response = await httpClient.PostAsync(
                new Uri(options.BaseAddress!, "infer"),
                content,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new EncoderInferenceUnavailableException(
                $"The encoder runtime at '{options.BaseAddress}' is " +
                $"unreachable: {exception.Message}");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new EncoderInferenceException(
                "The encoder runtime did not answer within the configured " +
                $"timeout of {options.Timeout}.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new EncoderInferenceException(
                    $"The encoder runtime answered {(int)response.StatusCode} " +
                    $"for model '{request.ModelId}'.");
            }

            InferResponse? payload;

            try
            {
                payload = await response.Content.ReadFromJsonAsync<InferResponse>(
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new EncoderInferenceException(
                    "The encoder runtime returned output that is not valid " +
                    "JSON.",
                    exception);
            }

            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.ModelId) ||
                string.IsNullOrWhiteSpace(payload.ModelVersion) ||
                payload.Scores is null)
            {
                throw new EncoderInferenceException(
                    "The encoder runtime returned an incomplete response.");
            }

            return new EncoderInferenceResult(
                payload.ModelId,
                payload.ModelVersion,
                payload.Scores,
                payload.DurationMilliseconds);
        }
    }

    #endregion

    #region Nested Types

    private sealed record InferPayload(
        [property: JsonPropertyName("modelId")] string ModelId,
        [property: JsonPropertyName("input")] string Input);

    private sealed record InferResponse(
        [property: JsonPropertyName("modelId")] string? ModelId,
        [property: JsonPropertyName("modelVersion")] string? ModelVersion,
        [property: JsonPropertyName("scores")] IReadOnlyList<double>? Scores,
        [property: JsonPropertyName("durationMilliseconds")] double DurationMilliseconds);

    #endregion
}
