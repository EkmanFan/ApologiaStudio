using System.Net;
using System.Text;
using System.Text.Json;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.AgentRuntime.Routing;

public sealed class AgentRegistryRoutingTests
{
    private static readonly AgentDescriptor PatristicsAgent = new(
        new AgentId(
            Guid.Parse("33333333-3333-3333-3333-333333333333")),
        "patristics",
        "Patristics Specialist");

    [Fact]
    public void Registry_ShouldResolveRegisteredAgentByIdAndSlug()
    {
        var registry = CreateRegistryWithPatristicsAgent();

        Assert.True(
            registry.TryGet(
                PatristicsAgent.Id,
                out var byId));
        Assert.Equal(PatristicsAgent, byId.Agent);

        Assert.True(
            registry.TryGet(
                "PATRISTICS",
                out var bySlug));
        Assert.Equal(PatristicsAgent, bySlug.Agent);
    }

    [Fact]
    public void DeterministicRouter_ShouldAcceptExplicitAgentFromRegistry()
    {
        var registry = CreateRegistryWithPatristicsAgent();
        var router = new DeterministicAgentRouter(registry);

        var decision = router.Route(
            CreateRequest(
                "Parle-moi des Pères apostoliques.",
                PatristicsAgent.Id));

        Assert.Equal(PatristicsAgent.Id, decision.AgentId);
        Assert.Equal(PatristicsAgent.DisplayName, decision.AgentName);
        Assert.True(decision.WasExplicitlyRequested);
    }

    [Fact]
    public async Task HybridRouter_ShouldResolveSemanticAgentFromRegistry()
    {
        var registry = CreateRegistryWithPatristicsAgent();
        var classifier = new StubSemanticClassifier(
            new SemanticRoutingResult(
                PatristicsAgent.Slug,
                0.99,
                "La demande relève de la patristique."));
        var router = new HybridAgentRouter(
            new DeterministicAgentRouter(registry),
            classifier,
            new HybridRoutingOptions(),
            registry);

        var decision = await router.RouteAsync(
            CreateRequest("Aide-moi sur ce sujet spécialisé."),
            CancellationToken.None);

        Assert.Equal(PatristicsAgent.Id, decision.AgentId);
        Assert.Equal(PatristicsAgent.DisplayName, decision.AgentName);
        Assert.Equal(1, classifier.CallCount);
    }

    [Fact]
    public async Task SemanticClassifier_ShouldBuildPromptAndSchemaFromRegistry()
    {
        const string payload = """
            {
              "agent": "patristics",
              "intent": "general",
              "confidence": 0.99,
              "reason": "La demande relève de la patristique.",
              "bibleReference": null
            }
            """;
        var registry = CreateRegistryWithPatristicsAgent();
        var handler = new StubHttpMessageHandler(
            CreateOllamaResponse(payload));
        var options = new OllamaRoutingOptions
        {
            BaseAddress = new Uri("http://127.0.0.1:11434/"),
            Model = "qwen3:8b",
            RequestTimeout = TimeSpan.FromSeconds(30),
            KeepAlive = "1m"
        };

        using var classifier = new OllamaSemanticRoutingClassifier(
            new HttpClient(handler)
            {
                BaseAddress = options.BaseAddress,
                Timeout = options.RequestTimeout
            },
            options,
            registry.All);

        var result = await classifier.ClassifyAsync(
            "Que disent les Pères apostoliques ?",
            CancellationToken.None);

        Assert.Equal(PatristicsAgent.Slug, result.AgentSlug);

        using var requestDocument = JsonDocument.Parse(
            handler.RequestBody);
        var root = requestDocument.RootElement;
        var systemPrompt = root
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString();

        Assert.Contains(
            "patristics",
            systemPrompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "early Christian writers",
            systemPrompt,
            StringComparison.Ordinal);

        var agentValues = root
            .GetProperty("format")
            .GetProperty("properties")
            .GetProperty("agent")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        Assert.Contains(PatristicsAgent.Slug, agentValues);
        Assert.Contains(BuiltInAgents.Historian.Slug, agentValues);
        Assert.Contains(
            BuiltInAgents.ProtestantApologist.Slug,
            agentValues);
    }

    private static BuiltInAgentRegistry CreateRegistryWithPatristicsAgent()
    {
        var builtIns = new BuiltInAgentRegistry();
        return new BuiltInAgentRegistry(
            builtIns.All.Concat(
            [
                new AgentRoutingProfile(
                    PatristicsAgent,
                    "- early Christian writers, Church Fathers and patristic sources;")
            ]));
    }

    private static AgentTurnRequest CreateRequest(
        string content,
        AgentId? requestedAgentId = null)
    {
        var messageId = MessageId.New();
        return new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            messageId,
            requestedAgentId,
            History:
            [
                new ConversationMessageContext(
                    messageId,
                    MessageRole.User,
                    content,
                    AgentId: null,
                    DateTimeOffset.UtcNow)
            ]);
    }

    private static string CreateOllamaResponse(string payload)
    {
        return JsonSerializer.Serialize(
            new
            {
                message = new
                {
                    role = "assistant",
                    content = payload
                },
                done = true
            });
    }

    private sealed class StubSemanticClassifier(
        SemanticRoutingResult result)
        : ISemanticRoutingClassifier
    {
        public int CallCount { get; private set; }

        public ValueTask<SemanticRoutingResult> ClassifyAsync(
            string userMessage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubHttpMessageHandler(
        string responseBody)
        : HttpMessageHandler
    {
        public string RequestBody { get; private set; } =
            string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(
                    cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
