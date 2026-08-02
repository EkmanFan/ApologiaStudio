using System.Text.Json;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using Xunit.Abstractions;

namespace ApologiaStudio.Evaluations.Routing;

public sealed class RoutingEvaluationTests(
    ITestOutputHelper output)
{
    [Fact]
    public void Dataset_ShouldContainTheClovisRegressionCase()
    {
        var cases = LoadCases();

        Assert.True(
            cases.Count >= 10);

        var clovisCase =
            Assert.Single(cases, item =>
                        item.Id ==
                            "historian-clovis-age");

        Assert.Equal(
            "historian",
            clovisCase.ExpectedAgent);
    }

    [Trait("Category", "LocalModel")]
    [Fact]
    public async Task OllamaHybridRouter_ShouldMeetMinimumAccuracy_WhenEnabled()
    {
        if (!LocalEvaluationsAreEnabled())
        {
            output.WriteLine(
                "Local model evaluation was not enabled.");

            return;
        }

        using var classifier =
            CreateClassifier();

        var router =
            new HybridAgentRouter(
                new DeterministicAgentRouter(),
                classifier,
                new HybridRoutingOptions());

        var cases = LoadCases();
        var successfulCases = 0;
        var failures = new List<string>();

        foreach (var evaluationCase in cases)
        {
            var decision =
                await router.RouteAsync(
                    CreateRequest(
                        evaluationCase.Question),
                    CancellationToken.None);

            Assert.True(
                BuiltInAgents.TryGet(
                    decision.AgentId,
                    out var selectedAgent));

            output.WriteLine(
                $"{evaluationCase.Id}: " +
                $"{selectedAgent.Slug}, " +
                $"confidence={decision.Confidence:F2}, " +
                $"reason={decision.Reason}");

            if (selectedAgent.Slug ==
                evaluationCase.ExpectedAgent)
            {
                successfulCases++;
            }
            else
            {
                failures.Add(
                    $"{evaluationCase.Id}: expected " +
                    $"{evaluationCase.ExpectedAgent}, got " +
                    $"{selectedAgent.Slug}; confidence=" +
                    $"{decision.Confidence:F2}; reason=" +
                    decision.Reason);
            }
        }

        var accuracy =
            (double)successfulCases /
            cases.Count;

        Assert.True(
            accuracy >= 0.80,
            $"Routing accuracy was {accuracy:P0}. " +
            string.Join("; ", failures));
    }

    private static OllamaSemanticRoutingClassifier
        CreateClassifier()
    {
        var baseUrl =
            Environment.GetEnvironmentVariable(
                "OLLAMA_BASE_URL")
            ?? "http://127.0.0.1:11434";

        var model =
            Environment.GetEnvironmentVariable(
                "OLLAMA_ROUTING_MODEL")
            ?? "qwen3:8b";

        var options =
            new OllamaRoutingOptions
            {
                BaseAddress =
                    new Uri(
                        baseUrl.TrimEnd('/') + "/"),
                Model = model,
                RequestTimeout =
                    TimeSpan.FromSeconds(60),
                KeepAlive = "10m"
            };

        var client =
            new HttpClient
            {
                BaseAddress =
                    options.BaseAddress,
                Timeout =
                    options.RequestTimeout
            };

        return new OllamaSemanticRoutingClassifier(
            client,
            options);
    }

    private static bool LocalEvaluationsAreEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(
                "OLLAMA_EVALUATIONS_ENABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<RoutingEvaluationCase>
        LoadCases()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Routing",
            "routing-cases.json");

        var json =
            File.ReadAllText(path);

        return JsonSerializer.Deserialize<
                List<RoutingEvaluationCase>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw new InvalidOperationException(
                "The routing evaluation dataset could not be loaded.");
    }

    private static AgentTurnRequest CreateRequest(
        string content)
    {
        var messageId = MessageId.New();

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
                    content,
                    AgentId: null,
                    DateTimeOffset.UtcNow)
            ]);
    }

    private sealed record RoutingEvaluationCase(
        string Id,
        string Question,
        string ExpectedAgent);
}
