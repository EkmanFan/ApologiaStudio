using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Application.Knowledge.MetadataReview;

namespace ApologiaStudio.IntegrationTests.AiRuntime;

/// <summary>
/// Exercises the non-streaming structured path against a real Ollama instance.
/// Skipped when Ollama is unavailable: the assistant must never be a
/// prerequisite for the rest of the suite.
/// </summary>
public sealed class OllamaStructuredGenerationRuntimeTests
{
    private const string BaseAddress = "http://127.0.0.1:11434";

    private const string Model = "qwen3:8b";

    [Fact]
    public async Task Schema_constrained_generation_returns_parseable_json()
    {
        if (!await OllamaIsAvailableAsync())
        {
            return;
        }

        var telemetry = new RecordingTelemetry();
        var runtime = CreateRuntime(telemetry);

        var result = await runtime.GenerateAsync(
            new StructuredGenerationRequest(
                "integration-test",
                "Answer only with the JSON object.",
                "Title: The Case for the Resurrection of Jesus.",
                Schema),
            CancellationToken.None);

        Assert.Equal(Model, result.Model);
        Assert.False(string.IsNullOrWhiteSpace(result.Json));

        using var document = System.Text.Json.JsonDocument.Parse(result.Json);
        Assert.True(document.RootElement.TryGetProperty("insufficientEvidence", out _));

        // Diagnostics must match the streaming path: both ends observed, with
        // the provider's own token counts carried through.
        Assert.Equal(1, telemetry.Started);
        Assert.Equal(1, telemetry.Completed);
        Assert.Equal(0, telemetry.Failed);
        Assert.NotNull(result.DoneReason);
        Assert.True(result.PromptTokenCount > 0);
        Assert.True(result.DurationMilliseconds > 0);
    }

    [Fact]
    public async Task Cancellation_is_honoured_and_reported()
    {
        if (!await OllamaIsAvailableAsync())
        {
            return;
        }

        var telemetry = new RecordingTelemetry();
        var runtime = CreateRuntime(telemetry);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runtime.GenerateAsync(
                new StructuredGenerationRequest(
                    "integration-test",
                    "Answer only with the JSON object.",
                    "Title: anything.",
                    Schema),
                cancellation.Token));

        Assert.Equal(1, telemetry.Failed);
        Assert.Equal("cancelled", telemetry.LastFailureReason);
    }

    [Fact]
    public async Task An_exhausted_timeout_is_reported_as_such()
    {
        // Deterministic: a listener that accepts the connection and never
        // answers, so the configured timeout is what ends the call rather than
        // how fast a model happens to be.
        using var listener = new SilentListener();

        var telemetry = new RecordingTelemetry();
        var runtime = new OllamaStructuredGenerationRuntime(
            new StaticSettingsStore(1, listener.BaseAddress),
            new DirectHttpClientFactory(),
            telemetry);

        var exception = await Assert.ThrowsAsync<StructuredGenerationException>(
            () => runtime.GenerateAsync(
                new StructuredGenerationRequest(
                    "integration-test",
                    "system",
                    "user",
                    Schema),
                CancellationToken.None));

        Assert.Contains("timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, telemetry.Failed);
        Assert.Equal("timeout", telemetry.LastFailureReason);
    }

    [Fact]
    public async Task An_invalid_schema_fails_before_any_call()
    {
        var telemetry = new RecordingTelemetry();
        var runtime = CreateRuntime(telemetry);

        await Assert.ThrowsAsync<StructuredGenerationException>(
            () => runtime.GenerateAsync(
                new StructuredGenerationRequest(
                    "integration-test",
                    "system",
                    "user",
                    "{ not a schema"),
                CancellationToken.None));

        Assert.Equal(0, telemetry.Started);
    }

    private static OllamaStructuredGenerationRuntime CreateRuntime(
        IStructuredGenerationTelemetry telemetry,
        int generationTimeoutSeconds = 120)
    {
        return new OllamaStructuredGenerationRuntime(
            new StaticSettingsStore(generationTimeoutSeconds),
            new DirectHttpClientFactory(),
            telemetry);
    }

    private static async Task<bool> OllamaIsAvailableAsync()
    {
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(BaseAddress),
                Timeout = TimeSpan.FromSeconds(2)
            };

            using var response = await client.GetAsync("api/version");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private const string Schema =
        """
        {
          "type": "object",
          "properties": {
            "suggested": { "type": "array", "items": { "type": "string" } },
            "insufficientEvidence": { "type": "boolean" }
          },
          "required": ["suggested", "insufficientEvidence"]
        }
        """;

    private sealed class StaticSettingsStore(
        int generationTimeoutSeconds,
        string? baseAddress = null)
        : IAiRuntimeSettingsStore
    {
        public Task<AiRuntimeSettingsSnapshot?> GetAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<AiRuntimeSettingsSnapshot?>(
                new AiRuntimeSettingsSnapshot(
                    AiRuntimeSettingsSnapshot.OllamaProvider,
                    baseAddress ?? BaseAddress,
                    Model,
                    Model,
                    30,
                    generationTimeoutSeconds,
                    "5m",
                    24,
                    24_000,
                    400,
                    DateTimeOffset.UtcNow,
                    new Dictionary<Guid, string>()));
        }

        public Task SaveAsync(
            AiRuntimeSettingsSnapshot settings,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DirectHttpClientFactory : IOllamaHttpClientFactory
    {
        public HttpClient Create(Uri baseAddress, TimeSpan timeout)
        {
            return new HttpClient
            {
                BaseAddress = baseAddress,
                Timeout = timeout
            };
        }
    }

    /// <summary>
    /// Accepts a connection and never answers, so a client timeout is the only
    /// possible outcome.
    /// </summary>
    private sealed class SilentListener : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener;

        public SilentListener()
        {
            _listener = new System.Net.Sockets.TcpListener(
                System.Net.IPAddress.Loopback,
                0);
            _listener.Start();

            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        await _listener.AcceptTcpClientAsync();
                    }
                }
                catch
                {
                    // The listener was disposed; nothing to recover.
                }
            });
        }

        public string BaseAddress =>
            $"http://127.0.0.1:{((System.Net.IPEndPoint)_listener.LocalEndpoint).Port}";

        public void Dispose() => _listener.Stop();
    }

    private sealed class RecordingTelemetry : IStructuredGenerationTelemetry
    {
        public int Started { get; private set; }

        public int Completed { get; private set; }

        public int Failed { get; private set; }

        public string? LastFailureReason { get; private set; }

        public void GenerationStarted(
            StructuredGenerationStartedObservation observation) => Started++;

        public void GenerationCompleted(
            StructuredGenerationCompletedObservation observation) => Completed++;

        public void GenerationFailed(
            StructuredGenerationFailedObservation observation)
        {
            Failed++;
            LastFailureReason = observation.Reason;
        }
    }
}
