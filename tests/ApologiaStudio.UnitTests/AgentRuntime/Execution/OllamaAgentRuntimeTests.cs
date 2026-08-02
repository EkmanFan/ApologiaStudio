using System.Net;
using System.Text;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Execution;

public sealed class OllamaAgentRuntimeTests
{
    [Fact]
    public async Task RunTurnAsync_ShouldStreamAndCompleteResponse()
    {
        const string responseBody = """
            {"message":{"role":"assistant","content":"Clovis "},"done":false}
            {"message":{"role":"assistant","content":"avait environ trente ans."},"done":false}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop"}
            """;

        var handler =
            new StubHttpMessageHandler(
                responseBody);

        var options =
            CreateOptions();

        using var runtime =
            new OllamaAgentRuntime(
                new StubAgentRouter(
                    new RoutingDecision(
                        BuiltInAgents.Historian.Id,
                        BuiltInAgents.Historian.DisplayName,
                        "Historical question.",
                        0.95,
                        WasExplicitlyRequested: false)),
                new AgentPromptCatalog(),
                new HttpClient(handler)
                {
                    BaseAddress =
                        options.BaseAddress,
                    Timeout =
                        options.RequestTimeout
                },
                options);

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

        Assert.NotNull(
            handler.RequestBody);

        Assert.Contains(
            "\"stream\":true",
            handler.RequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"think\":false",
            handler.RequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"model\":\"qwen3:8b\"",
            handler.RequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"role\":\"system\"",
            handler.RequestBody,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"role\":\"user\"",
            handler.RequestBody,
            StringComparison.Ordinal);
    }

    private static OllamaGenerationOptions
        CreateOptions()
    {
        return new OllamaGenerationOptions
        {
            BaseAddress =
                new Uri(
                    "http://127.0.0.1:11434/"),
            Model =
                "qwen3:8b",
            RequestTimeout =
                TimeSpan.FromSeconds(30),
            KeepAlive =
                "1m",
            MaximumHistoryMessages =
                10,
            MaximumHistoryCharacters =
                10_000,
            MaximumOutputTokens =
                500
        };
    }

    private static AgentTurnRequest
        CreateRequest()
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
            ]);
    }

    private sealed class StubAgentRouter(
        RoutingDecision decision)
        : IAgentRouter
    {
        public ValueTask<RoutingDecision> RouteAsync(
            AgentTurnRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                decision);
        }
    }

    private sealed class StubHttpMessageHandler(
        string responseBody)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            RequestUri =
                request.RequestUri;

            RequestBody =
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(
                        cancellationToken);

            return new HttpResponseMessage(
                HttpStatusCode.OK)
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
