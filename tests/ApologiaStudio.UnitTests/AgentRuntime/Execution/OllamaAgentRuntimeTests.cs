using System.Net;
using System.Text;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Execution;

public sealed class OllamaAgentRuntimeTests
{
    [Fact]
    public async Task RunTurnAsync_ShouldUseAssignedAgentModel()
    {
        const string responseBody = """
            {"message":{"role":"assistant","content":"Clovis "},"done":false}
            {"message":{"role":"assistant","content":"avait environ trente ans."},"done":false}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","total_duration":250000000,"load_duration":50000000,"prompt_eval_count":42,"prompt_eval_duration":30000000,"eval_count":17,"eval_duration":170000000}
            """;

        var handler =
            new StubHttpMessageHandler(
                responseBody);

        var settings =
            CreateSettings(
                new Dictionary<Guid, string>
                {
                    [BuiltInAgents.Historian.Id.Value] =
                        "mixtral:instruct"
                });

        var router =
            new StubAgentRouter(
                new RoutingDecision(
                    BuiltInAgents.Historian.Id,
                    BuiltInAgents.Historian.DisplayName,
                    "Historical question.",
                    0.95,
                    WasExplicitlyRequested: false));

        var telemetry =
            new RecordingOllamaRuntimeTelemetry();

        var runtime =
            new OllamaAgentRuntime(
                router,
                new AgentPromptCatalog(),
                new StubAiRuntimeSettingsStore(settings),
                new StubOllamaHttpClientFactory(handler),
                telemetry);

        var events =
            new List<AgentRunEvent>();

        await foreach (var runEvent in
                       runtime.RunTurnAsync(
                           CreateRequest(),
                           CancellationToken.None))
        {
            events.Add(runEvent);
        }

        Assert.Collection(
            events,
            selected =>
            {
                var value =
                    Assert.IsType<AgentSelectedEvent>(
                        selected);

                Assert.Equal(
                    BuiltInAgents.Historian.Id,
                    value.AgentId);
            },
            firstDelta =>
                Assert.Equal(
                    "Clovis ",
                    Assert.IsType<TextDeltaEvent>(
                        firstDelta).Content),
            secondDelta =>
                Assert.Equal(
                    "avait environ trente ans.",
                    Assert.IsType<TextDeltaEvent>(
                        secondDelta).Content),
            completed =>
                Assert.Equal(
                    "Clovis avait environ trente ans.",
                    Assert.IsType<AgentTurnCompletedEvent>(
                        completed).Content));

        Assert.Equal(
            "http://127.0.0.1:11434/api/chat",
            handler.RequestUri?.ToString());

        Assert.NotNull(handler.RequestBody);

        Assert.Contains(
            "\"model\":\"mixtral:instruct\"",
            handler.RequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"stream\":true",
            handler.RequestBody,
            StringComparison.Ordinal);

        Assert.Equal(1, router.CallCount);

        var firstToken = Assert.Single(telemetry.FirstTokens);
        Assert.Equal("mixtral:instruct", firstToken.Model);
        Assert.Equal(BuiltInAgents.Historian.Id, firstToken.AgentId);
        Assert.True(firstToken.TimeToFirstTokenMilliseconds >= 0);

        var started = Assert.Single(telemetry.Started);
        Assert.Equal("mixtral:instruct", started.Model);
        Assert.Equal(BuiltInAgents.Historian.Id, started.AgentId);

        var completed = Assert.Single(telemetry.Completed);
        Assert.Equal("stop", completed.DoneReason);
        Assert.Equal(42, completed.PromptTokenCount);
        Assert.Equal(17, completed.OutputTokenCount);
        Assert.Equal(250_000_000, completed.TotalDurationNanoseconds);
        Assert.Empty(telemetry.Rejected);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldUseDefaultModelWithoutAssignment()
    {
        const string responseBody = """
            {"message":{"role":"assistant","content":"Réponse."},"done":false}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop"}
            """;

        var handler =
            new StubHttpMessageHandler(responseBody);

        var router =
            new StubAgentRouter(
                new RoutingDecision(
                    BuiltInAgents.Historian.Id,
                    BuiltInAgents.Historian.DisplayName,
                    "Already routed.",
                    0.95,
                    WasExplicitlyRequested: false));

        var runtime =
            new OllamaAgentRuntime(
                router,
                new AgentPromptCatalog(),
                new StubAiRuntimeSettingsStore(
                    CreateSettings()),
                new StubOllamaHttpClientFactory(handler),
                new RecordingOllamaRuntimeTelemetry());

        var suppliedDecision =
            new RoutingDecision(
                BuiltInAgents.Historian.Id,
                BuiltInAgents.Historian.DisplayName,
                "Already routed.",
                0.95,
                WasExplicitlyRequested: false);

        await foreach (var _ in runtime.RunTurnAsync(
                           CreateRequest(
                               ApplicationLanguage.English),
                           suppliedDecision,
                           CancellationToken.None))
        {
        }

        Assert.Equal(0, router.CallCount);
        Assert.Contains(
            "\"model\":\"qwen3:8b\"",
            handler.RequestBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "default to English for theological responses",
            handler.RequestBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldApplyTheologicalLanguageToApologistPrompt()
    {
        const string responseBody = """
            {"message":{"role":"assistant","content":"Answer."},"done":false}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop"}
            """;

        var handler =
            new StubHttpMessageHandler(responseBody);

        var router =
            new StubAgentRouter(
                new RoutingDecision(
                    BuiltInAgents.ProtestantApologist.Id,
                    BuiltInAgents.ProtestantApologist.DisplayName,
                    "Apologetics question.",
                    0.95,
                    WasExplicitlyRequested: false));

        var runtime =
            new OllamaAgentRuntime(
                router,
                new AgentPromptCatalog(),
                new StubAiRuntimeSettingsStore(
                    CreateSettings()),
                new StubOllamaHttpClientFactory(handler),
                new RecordingOllamaRuntimeTelemetry());

        await foreach (var _ in runtime.RunTurnAsync(
                           CreateRequest(
                               ApplicationLanguage.English),
                           CancellationToken.None))
        {
        }

        Assert.Contains(
            "default to English for theological responses",
            handler.RequestBody,
            StringComparison.Ordinal);
    }


    [Fact]
    public async Task RunTurnAsync_ShouldRejectRepeatedGeneration()
    {
        const string repeated = "co-éternelles, ";

        var responseBody =
            string.Join(
                "\n",
                new[]
                {
                    "{\"message\":{\"role\":\"assistant\",\"content\":\"Les personnes sont \"},\"done\":false}",
                    $"{{\"message\":{{\"role\":\"assistant\",\"content\":\"{repeated}\"}},\"done\":false}}",
                    $"{{\"message\":{{\"role\":\"assistant\",\"content\":\"{repeated}\"}},\"done\":false}}",
                    $"{{\"message\":{{\"role\":\"assistant\",\"content\":\"{repeated}\"}},\"done\":false}}",
                    $"{{\"message\":{{\"role\":\"assistant\",\"content\":\"{repeated}\"}},\"done\":false}}",
                    "{\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":true,\"done_reason\":\"stop\"}"
                });

        var handler =
            new StubHttpMessageHandler(responseBody);

        var telemetry =
            new RecordingOllamaRuntimeTelemetry();

        var runtime =
            new OllamaAgentRuntime(
                new StubAgentRouter(
                    new RoutingDecision(
                        BuiltInAgents.ProtestantApologist.Id,
                        BuiltInAgents.ProtestantApologist.DisplayName,
                        "Apologetics question.",
                        0.95,
                        WasExplicitlyRequested: false)),
                new AgentPromptCatalog(),
                new StubAiRuntimeSettingsStore(
                    CreateSettings()),
                new StubOllamaHttpClientFactory(handler),
                telemetry);

        var events =
            new List<AgentRunEvent>();

        var exception =
            await Assert.ThrowsAsync<
                OllamaRepetitionDetectedException>(
                async () =>
                {
                    await foreach (var runEvent in
                                   runtime.RunTurnAsync(
                                       CreateRequest(),
                                       CancellationToken.None))
                    {
                        events.Add(runEvent);
                    }
                });

        Assert.True(exception.RepeatCount >= 4);
        Assert.DoesNotContain(
            events,
            runEvent => runEvent is AgentTurnCompletedEvent);
        Assert.Single(telemetry.Rejected);
        Assert.Empty(telemetry.Completed);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldIsolateHistoryToSelectedAgentTurns()
    {
        const string responseBody = """
            {"message":{"role":"assistant","content":"Réponse courante."},"done":false}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","eval_count":5}
            """;

        var historianUserMessageId = MessageId.New();
        var historianAssistantMessageId = MessageId.New();
        var apologistUserMessageId = MessageId.New();
        var apologistAssistantMessageId = MessageId.New();
        var currentMessageId = MessageId.New();

        var request =
            new AgentTurnRequest(
                ConversationId.New(),
                UserId.New(),
                currentMessageId,
                RequestedAgentId: BuiltInAgents.ProtestantApologist.Id,
                History:
                [
                    new ConversationMessageContext(
                        historianUserMessageId,
                        MessageRole.User,
                        "HISTORIAN-QUESTION-MARKER",
                        AgentId: null,
                        DateTimeOffset.UtcNow.AddMinutes(-5)),
                    new ConversationMessageContext(
                        historianAssistantMessageId,
                        MessageRole.Agent,
                        "HISTORIAN-ANSWER-MARKER",
                        BuiltInAgents.Historian.Id,
                        DateTimeOffset.UtcNow.AddMinutes(-4)),
                    new ConversationMessageContext(
                        apologistUserMessageId,
                        MessageRole.User,
                        "APOLOGIST-QUESTION-MARKER",
                        AgentId: null,
                        DateTimeOffset.UtcNow.AddMinutes(-3)),
                    new ConversationMessageContext(
                        apologistAssistantMessageId,
                        MessageRole.Agent,
                        "APOLOGIST-ANSWER-MARKER",
                        BuiltInAgents.ProtestantApologist.Id,
                        DateTimeOffset.UtcNow.AddMinutes(-2)),
                    new ConversationMessageContext(
                        currentMessageId,
                        MessageRole.User,
                        "CURRENT-QUESTION-MARKER",
                        AgentId: null,
                        DateTimeOffset.UtcNow)
                ],
                ApplicationLanguage.French);

        var handler =
            new StubHttpMessageHandler(responseBody);

        var runtime =
            new OllamaAgentRuntime(
                new StubAgentRouter(
                    new RoutingDecision(
                        BuiltInAgents.ProtestantApologist.Id,
                        BuiltInAgents.ProtestantApologist.DisplayName,
                        "Explicit selection.",
                        1.0,
                        WasExplicitlyRequested: true)),
                new AgentPromptCatalog(),
                new StubAiRuntimeSettingsStore(
                    CreateSettings()),
                new StubOllamaHttpClientFactory(handler),
                new RecordingOllamaRuntimeTelemetry());

        await foreach (var _ in runtime.RunTurnAsync(
                           request,
                           CancellationToken.None))
        {
        }

        Assert.NotNull(handler.RequestBody);
        Assert.DoesNotContain(
            "HISTORIAN-QUESTION-MARKER",
            handler.RequestBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "HISTORIAN-ANSWER-MARKER",
            handler.RequestBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "APOLOGIST-QUESTION-MARKER",
            handler.RequestBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "APOLOGIST-ANSWER-MARKER",
            handler.RequestBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "CURRENT-QUESTION-MARKER",
            handler.RequestBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldExcludeRepetitiveAssistantHistory()
    {
        const string responseBody = """
            {"message":{"role":"assistant","content":"Réponse saine."},"done":false}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","eval_count":5}
            """;

        const string corruptedHistory =
            "co-éternelles, co-éternelles, co-éternelles, " +
            "co-éternelles, co-éternelles, co-éternelles, ";

        var previousUserMessageId = MessageId.New();
        var previousAssistantMessageId = MessageId.New();
        var currentMessageId = MessageId.New();
        var conversationId = ConversationId.New();

        var request =
            new AgentTurnRequest(
                conversationId,
                UserId.New(),
                currentMessageId,
                RequestedAgentId: null,
                History:
                [
                    new ConversationMessageContext(
                        previousUserMessageId,
                        MessageRole.User,
                        "Explique la Trinité.",
                        AgentId: null,
                        DateTimeOffset.UtcNow.AddMinutes(-2)),
                    new ConversationMessageContext(
                        previousAssistantMessageId,
                        MessageRole.Agent,
                        corruptedHistory,
                        BuiltInAgents.ProtestantApologist.Id,
                        DateTimeOffset.UtcNow.AddMinutes(-1)),
                    new ConversationMessageContext(
                        currentMessageId,
                        MessageRole.User,
                        "Recommence plus simplement.",
                        AgentId: null,
                        DateTimeOffset.UtcNow)
                ],
                ApplicationLanguage.French);

        var handler =
            new StubHttpMessageHandler(responseBody);

        var telemetry =
            new RecordingOllamaRuntimeTelemetry();

        var runtime =
            new OllamaAgentRuntime(
                new StubAgentRouter(
                    new RoutingDecision(
                        BuiltInAgents.ProtestantApologist.Id,
                        BuiltInAgents.ProtestantApologist.DisplayName,
                        "Apologetics question.",
                        0.95,
                        WasExplicitlyRequested: false)),
                new AgentPromptCatalog(),
                new StubAiRuntimeSettingsStore(
                    CreateSettings()),
                new StubOllamaHttpClientFactory(handler),
                telemetry);

        await foreach (var _ in runtime.RunTurnAsync(
                           request,
                           CancellationToken.None))
        {
        }

        Assert.DoesNotContain(
            corruptedHistory,
            handler.RequestBody,
            StringComparison.Ordinal);

        var skipped =
            Assert.Single(telemetry.HistorySkipped);

        Assert.Equal(
            previousAssistantMessageId,
            skipped.MessageId);
    }

    private static AiRuntimeSettingsSnapshot CreateSettings(
        IReadOnlyDictionary<Guid, string>? agentModels = null)
    {
        return new AiRuntimeSettingsSnapshot(
            AiRuntimeSettingsSnapshot.OllamaProvider,
            "http://127.0.0.1:11434/",
            "qwen3:8b",
            "qwen3:8b",
            RoutingTimeoutSeconds: 60,
            GenerationTimeoutSeconds: 30,
            KeepAlive: "1m",
            MaximumHistoryMessages: 10,
            MaximumHistoryCharacters: 10_000,
            MaximumOutputTokens: 500,
            UpdatedAt: DateTimeOffset.UtcNow,
            AgentModels:
                agentModels ??
                new Dictionary<Guid, string>());
    }

    private static AgentTurnRequest CreateRequest(
        ApplicationLanguage theologicalLanguage =
            ApplicationLanguage.French)
    {
        var messageId =
            MessageId.New();

        return new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            messageId,
            RequestedAgentId: null,
            History:
            [
                new ConversationMessageContext(
                    messageId,
                    MessageRole.User,
                    "Quel âge avait Clovis lors de son sacre ?",
                    AgentId: null,
                    DateTimeOffset.UtcNow)
            ],
            theologicalLanguage);
    }

    private sealed class StubAgentRouter(
        RoutingDecision decision)
        : IAgentRouter
    {
        public int CallCount { get; private set; }

        public ValueTask<RoutingDecision> RouteAsync(
            AgentTurnRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            return ValueTask.FromResult(decision);
        }
    }

    private sealed class StubAiRuntimeSettingsStore(
        AiRuntimeSettingsSnapshot settings)
        : IAiRuntimeSettingsStore
    {
        public Task<AiRuntimeSettingsSnapshot?> GetAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<AiRuntimeSettingsSnapshot?>(settings);
        }

        public Task SaveAsync(
            AiRuntimeSettingsSnapshot value,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubOllamaHttpClientFactory(
        HttpMessageHandler handler)
        : IOllamaHttpClientFactory
    {
        public HttpClient Create(
            Uri baseAddress,
            TimeSpan timeout)
        {
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = baseAddress,
                Timeout = timeout
            };
        }
    }


    private sealed class RecordingOllamaRuntimeTelemetry
        : IOllamaRuntimeTelemetry
    {
        public List<OllamaGenerationFirstTokenObservation> FirstTokens { get; } = [];

        public List<OllamaGenerationStartedObservation> Started { get; } = [];

        public List<OllamaGenerationCompletedObservation> Completed { get; } = [];

        public List<OllamaGenerationRejectedObservation> Rejected { get; } = [];

        public List<OllamaHistoryMessageSkippedObservation> HistorySkipped { get; } = [];

        public void GenerationFirstToken(
            OllamaGenerationFirstTokenObservation observation)
        {
            FirstTokens.Add(observation);
        }

        public void GenerationStarted(
            OllamaGenerationStartedObservation observation)
        {
            Started.Add(observation);
        }

        public void GenerationCompleted(
            OllamaGenerationCompletedObservation observation)
        {
            Completed.Add(observation);
        }

        public void GenerationRejected(
            OllamaGenerationRejectedObservation observation)
        {
            Rejected.Add(observation);
        }

        public void HistoryMessageSkipped(
            OllamaHistoryMessageSkippedObservation observation)
        {
            HistorySkipped.Add(observation);
        }
    }

    private sealed class StubHttpMessageHandler(
        string responseBody)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;

            RequestBody =
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(
                        cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content =
                    new StringContent(
                        responseBody,
                        Encoding.UTF8,
                        "application/x-ndjson")
            };
        }
    }
}
