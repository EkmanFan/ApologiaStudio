using System.Diagnostics;
using System.Text.Json;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.AgentRuntime.Routing.Semantic;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Evaluations.Support;
using Xunit.Abstractions;

namespace ApologiaStudio.Evaluations.Routing;

[Collection(LocalModelEvaluationCollection.Name)]
public sealed class DynamicRoutingEvaluationTests(
    ITestOutputHelper output)
{
    private static readonly AgentDescriptor ArgumentAnalyst =
        new(
            new AgentId(
                Guid.Parse(
                    "33333333-3333-3333-3333-333333333333")),
            "argument-analyst",
            "Argument Analyst");

    private static readonly AgentRoutingProfile ArgumentAnalystProfile =
        new(
            ArgumentAnalyst,
            """
            - formal and informal logic, argument mapping and validity;
            - identify premises, conclusions, assumptions and fallacies;
            - evaluate whether a conclusion follows from premises;
            - analyse argument structure without defending a theological claim;
            - route defence or refutation of Christian doctrine to the Protestant apologist.
            """);

    [Fact]
    public void Dataset_ShouldCoverCustomAndAmbiguousRouting()
    {
        var cases = LoadCases();

        Assert.True(cases.Count >= 8);
        Assert.Contains(
            cases,
            candidate =>
                candidate.ExpectedAgent ==
                ArgumentAnalyst.Slug);
        Assert.True(
            cases.Count(candidate =>
                candidate.Category == "ambiguous") >= 4);
    }

    [Fact]
    public async Task HybridRouter_ShouldSelectCustomAgent_WhenSemanticClassifierChoosesIt()
    {
        var classifier =
            new RecordingSemanticClassifier(
                new SemanticRoutingResult(
                    ArgumentAnalyst.Slug,
                    0.95,
                    "Argument structure question."));

        var router =
            CreateRouter(
                classifier,
                CreateProfiles());

        var decision =
            await router.RouteAsync(
                CreateRequest(
                    "Analyse la validité de cet argument."),
                CancellationToken.None);

        Assert.Equal(
            ArgumentAnalyst.Id,
            decision.AgentId);

        var observedProfiles =
            Assert.Single(classifier.ObservedProfiles);

        Assert.Contains(
            observedProfiles,
            profile =>
                profile.Agent.Id ==
                    ArgumentAnalyst.Id &&
                profile.RoutingDescription.Contains(
                    "argument mapping",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task HybridRouter_ShouldUseFreshRoutingDescriptionOnEveryRoute()
    {
        var profiles = CreateProfiles().ToList();
        var provider =
            new MutableRoutingSnapshotProvider(profiles);
        var classifier =
            new RecordingSemanticClassifier(
                new SemanticRoutingResult(
                    ArgumentAnalyst.Slug,
                    0.95,
                    "Argument structure question."));
        var router =
            new HybridAgentRouter(
                new DeterministicAgentRouter(),
                classifier,
                new HybridRoutingOptions(),
                provider);

        await router.RouteAsync(
            CreateRequest("Analyse cet argument."),
            CancellationToken.None);

        profiles =
            CreateProfiles(
                argumentAnalystRoutingDescription:
                    "UPDATED-ROUTING-DESCRIPTION")
                .ToList();
        provider.SetProfiles(profiles);

        await router.RouteAsync(
            CreateRequest("Analyse cet autre argument."),
            CancellationToken.None);

        Assert.Equal(2, classifier.ObservedProfiles.Count);

        Assert.Contains(
            classifier.ObservedProfiles[0],
            profile =>
                profile.Agent.Id == ArgumentAnalyst.Id &&
                !profile.RoutingDescription.Contains(
                    "UPDATED-ROUTING-DESCRIPTION",
                    StringComparison.Ordinal));

        Assert.Contains(
            classifier.ObservedProfiles[1],
            profile =>
                profile.Agent.Id == ArgumentAnalyst.Id &&
                profile.RoutingDescription ==
                    "UPDATED-ROUTING-DESCRIPTION");
    }

    [Fact]
    public async Task HybridRouter_ShouldFallbackToDeterministic_WhenSemanticClassifierFails()
    {
        var profiles = CreateProfiles();
        var request = CreateRequest(
            "Quel âge avait Luther lors de la publication des 95 thèses ?");
        var expected = new DeterministicAgentRouter().Route(
            request,
            new AgentRegistry(profiles));
        var router =
            CreateRouter(
                new ThrowingSemanticClassifier(),
                profiles);
        var decision =
            await router.RouteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(expected.AgentId, decision.AgentId);
        Assert.Equal(expected.Confidence, decision.Confidence);
        Assert.Contains("deterministic fallback", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Category", "LocalModel")]
    [Trait("Benchmark", "ModelComparison")]
    [Fact]
    public async Task OllamaDynamicRouter_ShouldMeetMinimumAccuracy_WhenEnabled()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            output.WriteLine(
                "Local model evaluation was not enabled.");
            return;
        }

        var profiles = CreateProfiles();
        var classifier =
            new DiagnosticSemanticRoutingClassifier(
                new LocalOllamaSemanticClassifier());
        var router =
            CreateRouter(
                classifier,
                profiles);
        var cases = LoadCases();
        var successfulCases = 0;
        var failures = new List<string>();
        var routingDurations = new List<double>();
        var semanticErrors = 0;

        foreach (var evaluationCase in cases)
        {
            classifier.Reset();
            var startedAt = Stopwatch.GetTimestamp();
            var decision =
                await router.RouteAsync(
                    CreateRequest(
                        evaluationCase.Question),
                    CancellationToken.None);
            var duration =
                Stopwatch.GetElapsedTime(startedAt)
                    .TotalMilliseconds;

            routingDurations.Add(duration);
            if (classifier.LastException is not null)
            {
                semanticErrors++;
            }
            var selected =
                profiles.Single(
                    profile =>
                        profile.Agent.Id ==
                        decision.AgentId);

            var semanticError =
                classifier.LastException is null
                    ? "none"
                    : $"{classifier.LastException.GetType().Name}: " +
                      classifier.LastException.Message;

            output.WriteLine(
                $"{evaluationCase.Id}: " +
                $"expected={evaluationCase.ExpectedAgent}, " +
                $"actual={selected.Agent.Slug}, " +
                $"confidence={decision.Confidence:F2}, " +
                $"routingMs={duration:F1}, " +
                $"semanticMs={classifier.LastDurationMilliseconds:F1}, " +
                $"semanticError={semanticError}, " +
                $"reason={decision.Reason}");

            if (selected.Agent.Slug ==
                evaluationCase.ExpectedAgent)
            {
                successfulCases++;
                continue;
            }

            failures.Add(
                $"{evaluationCase.Id}: expected " +
                $"{evaluationCase.ExpectedAgent}, got " +
                $"{selected.Agent.Slug}; semanticError=" +
                semanticError);
        }

        var accuracy =
            (double)successfulCases /
            cases.Count;

        output.WriteLine(
            $"Dynamic routing accuracy: {accuracy:P0}");
        output.WriteLine(
            $"MODEL_ROUTING_SUMMARY|" +
            $"model={LocalModelEvaluationSupport.GetRoutingModel()}|" +
            $"cases={cases.Count}|" +
            $"accuracy={accuracy:F3}|" +
            $"semanticErrors={semanticErrors}|" +
            $"avgRoutingMs={routingDurations.Average():F1}");

        Assert.True(
            accuracy >= 0.75,
            $"Dynamic routing accuracy was {accuracy:P0}. " +
            string.Join("; ", failures));
    }

    private static HybridAgentRouter CreateRouter(
        ISemanticRoutingClassifier classifier,
        IReadOnlyList<AgentRoutingProfile> profiles) =>
        new(
            new DeterministicAgentRouter(),
            classifier,
            new HybridRoutingOptions(),
            new StaticRoutingSnapshotProvider(profiles));

    private static IReadOnlyList<AgentRoutingProfile> CreateProfiles(
        string? argumentAnalystRoutingDescription = null)
    {
        var customProfile =
            argumentAnalystRoutingDescription is null
                ? ArgumentAnalystProfile
                : new AgentRoutingProfile(
                    ArgumentAnalyst,
                    argumentAnalystRoutingDescription);

        return
        [
            .. BuiltInAgentRegistry.Profiles,
            customProfile
        ];
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

    private static IReadOnlyList<DynamicRoutingEvaluationCase>
        LoadCases()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Routing",
            "dynamic-routing-cases.json");
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<
                List<DynamicRoutingEvaluationCase>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw new InvalidOperationException(
                "The dynamic routing evaluation dataset could not be loaded.");
    }

    private sealed record DynamicRoutingEvaluationCase(
        string Id,
        string Question,
        string ExpectedAgent,
        string Category);

    private sealed class StaticRoutingSnapshotProvider(
        IReadOnlyList<AgentRoutingProfile> profiles)
        : IAgentRoutingSnapshotProvider
    {
        public ValueTask<IAgentRegistry> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IAgentRegistry>(
                new AgentRegistry(profiles));
        }
    }

    private sealed class MutableRoutingSnapshotProvider(
        IReadOnlyList<AgentRoutingProfile> profiles)
        : IAgentRoutingSnapshotProvider
    {
        private IReadOnlyList<AgentRoutingProfile> _profiles = profiles;

        public void SetProfiles(
            IReadOnlyList<AgentRoutingProfile> profiles) =>
            _profiles = profiles;

        public ValueTask<IAgentRegistry> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IAgentRegistry>(
                new AgentRegistry(_profiles));
        }
    }

    private sealed class RecordingSemanticClassifier(
        SemanticRoutingResult result)
        : ISemanticRoutingClassifier
    {
        public List<IReadOnlyList<AgentRoutingProfile>>
            ObservedProfiles { get; } = [];

        public ValueTask<SemanticRoutingResult> ClassifyAsync(
            string userMessage,
            IReadOnlyList<AgentRoutingProfile> routingProfiles,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObservedProfiles.Add(
                routingProfiles.ToArray());
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingSemanticClassifier
        : ISemanticRoutingClassifier
    {
        public ValueTask<SemanticRoutingResult> ClassifyAsync(
            string userMessage,
            IReadOnlyList<AgentRoutingProfile> routingProfiles,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "Synthetic semantic routing failure.");
    }

    private sealed class DiagnosticSemanticRoutingClassifier(
        ISemanticRoutingClassifier inner)
        : ISemanticRoutingClassifier
    {
        public Exception? LastException { get; private set; }

        public double LastDurationMilliseconds { get; private set; }

        public void Reset()
        {
            LastException = null;
            LastDurationMilliseconds = 0;
        }

        public async ValueTask<SemanticRoutingResult> ClassifyAsync(
            string userMessage,
            IReadOnlyList<AgentRoutingProfile> routingProfiles,
            CancellationToken cancellationToken)
        {
            var startedAt = Stopwatch.GetTimestamp();

            try
            {
                return await inner.ClassifyAsync(
                    userMessage,
                    routingProfiles,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                LastException = exception;
                throw;
            }
            finally
            {
                LastDurationMilliseconds =
                    Stopwatch.GetElapsedTime(startedAt)
                        .TotalMilliseconds;
            }
        }
    }

    private sealed class LocalOllamaSemanticClassifier
        : ISemanticRoutingClassifier
    {
        public async ValueTask<SemanticRoutingResult> ClassifyAsync(
            string userMessage,
            IReadOnlyList<AgentRoutingProfile> routingProfiles,
            CancellationToken cancellationToken)
        {
            var options =
                new OllamaRoutingOptions
                {
                    BaseAddress =
                        LocalModelEvaluationSupport.GetBaseAddress(),
                    Model =
                        LocalModelEvaluationSupport.GetRoutingModel(),
                    RequestTimeout =
                        TimeSpan.FromSeconds(60),
                    KeepAlive = "10m"
                };

            using var client =
                new HttpClient
                {
                    BaseAddress = options.BaseAddress,
                    Timeout = options.RequestTimeout
                };
            using var classifier =
                new OllamaSemanticRoutingClassifier(
                    client,
                    options,
                    routingProfiles);

            return await classifier.ClassifyAsync(
                userMessage,
                cancellationToken);
        }
    }
}
