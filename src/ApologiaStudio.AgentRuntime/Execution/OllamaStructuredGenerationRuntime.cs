using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.AiRuntime.Settings;

namespace ApologiaStudio.AgentRuntime.Execution;

/// <summary>
/// Non-streaming, schema-constrained generation over the same Ollama endpoint,
/// settings, HTTP client factory, timeout and cancellation semantics as the
/// conversational runtime.
///
/// It is a second transport shape, not a second inference path: it shares the
/// configured base address, generation timeout and diagnostics rather than
/// bypassing them.
/// </summary>
public sealed class OllamaStructuredGenerationRuntime(
    IAiRuntimeSettingsStore settingsStore,
    IOllamaHttpClientFactory httpClientFactory,
    IStructuredGenerationTelemetry telemetry)
    : IStructuredGenerationRuntime
{
    private const int MaximumErrorBodyLength = 2_000;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<StructuredGenerationResult> GenerateAsync(
        StructuredGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResponseSchema);

        var settings =
            await settingsStore.GetAsync(cancellationToken)
            ?? throw new StructuredGenerationException(
                "AI runtime settings have not been initialized.");

        var model = string.IsNullOrWhiteSpace(request.ModelOverride)
            ? settings.DefaultAgentModel
            : request.ModelOverride;

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new StructuredGenerationException(
                "No model is configured for structured generation.");
        }

        var maximumOutputTokens =
            request.MaximumOutputTokens ?? settings.MaximumOutputTokens;

        JsonElement schema;
        try
        {
            using var parsed = JsonDocument.Parse(request.ResponseSchema);
            schema = parsed.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new StructuredGenerationException(
                "The response schema is not valid JSON.",
                exception);
        }

        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            stream = false,
            think = false,
            keep_alive = settings.KeepAlive,
            format = schema,
            options = new
            {
                temperature = 0.2,
                num_predict = maximumOutputTokens
            }
        };

        var baseAddress =
            AiRuntimeSettingsValidator.NormalizeBaseAddress(settings.BaseAddress);

        using var httpClient =
            httpClientFactory.Create(
                baseAddress,
                TimeSpan.FromSeconds(settings.GenerationTimeoutSeconds));

        using var httpRequest =
            new HttpRequestMessage(HttpMethod.Post, "api/chat")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };

        telemetry.GenerationStarted(
            new StructuredGenerationStartedObservation(
                request.Purpose,
                model,
                maximumOutputTokens));

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody =
                    await response.Content.ReadAsStringAsync(cancellationToken);

                throw CreateHttpException(response.StatusCode, errorBody);
            }

            var payload =
                await response.Content.ReadFromJsonAsync<OllamaStructuredResponse>(
                    JsonOptions,
                    cancellationToken);

            var content = payload?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new StructuredGenerationException(
                    "Ollama returned an empty structured response.");
            }

            var elapsed =
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            telemetry.GenerationCompleted(
                new StructuredGenerationCompletedObservation(
                    request.Purpose,
                    model,
                    payload!.DoneReason,
                    payload.PromptEvalCount,
                    payload.EvalCount,
                    elapsed));

            return new StructuredGenerationResult(
                model,
                content,
                payload.DoneReason,
                payload.PromptEvalCount,
                payload.EvalCount,
                elapsed);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // A caller-initiated cancellation is not a runtime failure.
            telemetry.GenerationFailed(
                new StructuredGenerationFailedObservation(
                    request.Purpose,
                    model,
                    "cancelled",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
            throw;
        }
        catch (OperationCanceledException exception)
        {
            // HttpClient surfaces its own timeout the same way; the caller's
            // token is not the one that fired.
            telemetry.GenerationFailed(
                new StructuredGenerationFailedObservation(
                    request.Purpose,
                    model,
                    "timeout",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));

            throw new StructuredGenerationException(
                "Structured generation exceeded the configured timeout of " +
                $"{settings.GenerationTimeoutSeconds} seconds.",
                exception);
        }
        catch (Exception exception) when (exception is not StructuredGenerationException)
        {
            telemetry.GenerationFailed(
                new StructuredGenerationFailedObservation(
                    request.Purpose,
                    model,
                    exception.GetType().Name,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
            throw;
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

    private sealed class OllamaStructuredResponse
    {
        [JsonPropertyName("message")]
        public OllamaStructuredMessage? Message { get; init; }

        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; init; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; init; }
    }

    private sealed class OllamaStructuredMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
