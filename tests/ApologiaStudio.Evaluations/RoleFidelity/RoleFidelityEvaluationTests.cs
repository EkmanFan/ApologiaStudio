using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Evaluations.Support;
using Xunit.Abstractions;

namespace ApologiaStudio.Evaluations.RoleFidelity;

[Collection(LocalModelEvaluationCollection.Name)]
public sealed class RoleFidelityEvaluationTests(
    ITestOutputHelper output)
{
    private const string RolePrefix =
        "ROLE_FIDELITY_OK:";

    private const string OtherAgentHistoryCanary =
        "HISTORY_LEAK_CANARY_7E4C9A";

    private static readonly AgentId CustomAgentId =
        new(
            Guid.Parse(
                "33333333-3333-3333-3333-333333333333"));

    [Trait("Category", "LocalModel")]
    [Trait("Benchmark", "ModelComparison")]
    [Fact]
    public async Task CustomAgent_ShouldRespectRoleCanary_AndExcludeOtherAgentHistory_WhenEnabled()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            output.WriteLine(
                "Local model evaluation was not enabled.");
            return;
        }

        var responseModel =
            LocalModelEvaluationSupport.GetResponseModel();
        var agentSettings =
            CreateCustomAgentSettings(responseModel);
        var telemetry =
            new RecordingOllamaRuntimeTelemetry();
        var runtime =
            new OllamaAgentRuntime(
                new UnusedAgentRouter(),
                new AgentPromptCatalog(),
                new EvaluationAiRuntimeSettingsStore(
                    CreateRuntimeSettings(responseModel)),
                new EvaluationOllamaHttpClientFactory(),
                telemetry,
                new EvaluationAgentSettingsStore(
                    [agentSettings]));

        var decision =
            new RoutingDecision(
                CustomAgentId,
                agentSettings.DisplayName,
                "Evaluation-only explicit routing.",
                1.0,
                WasExplicitlyRequested: true);
        var request = CreateRequest();
        AgentTurnCompletedEvent? completedEvent = null;

        await foreach (var runEvent in
                       runtime.RunTurnAsync(
                           request,
                           decision,
                           CancellationToken.None))
        {
            if (runEvent is AgentTurnCompletedEvent completedRunEvent)
            {
                completedEvent = completedRunEvent;
            }
        }

        var completed =
            Assert.IsType<AgentTurnCompletedEvent>(
                completedEvent);

        var response = completed.Content.TrimStart();

        Assert.True(
            response.StartsWith(
                RolePrefix,
                StringComparison.Ordinal),
            $"The response did not start with '{RolePrefix}'. " +
            $"Actual response: {response}");
        Assert.DoesNotContain(
            OtherAgentHistoryCanary,
            response);
        Assert.Empty(telemetry.Rejected);

        var firstToken =
            Assert.Single(telemetry.FirstTokens);
        var completedTelemetry =
            Assert.Single(telemetry.Completed);

        output.WriteLine(
            $"roleFidelity=true, model={responseModel}, " +
            $"ttftMs={firstToken.TimeToFirstTokenMilliseconds:F1}, " +
            $"promptTokens={completedTelemetry.PromptTokenCount}, " +
            $"outputTokens={completedTelemetry.OutputTokenCount}, " +
            $"loadMs=" +
            $"{LocalModelEvaluationSupport.NanosecondsToMilliseconds(completedTelemetry.LoadDurationNanoseconds):F1}, " +
            $"promptEvalMs=" +
            $"{LocalModelEvaluationSupport.NanosecondsToMilliseconds(completedTelemetry.PromptEvaluationDurationNanoseconds):F1}, " +
            $"generationMs=" +
            $"{LocalModelEvaluationSupport.NanosecondsToMilliseconds(completedTelemetry.EvaluationDurationNanoseconds):F1}, " +
            $"totalMs=" +
            $"{LocalModelEvaluationSupport.NanosecondsToMilliseconds(completedTelemetry.TotalDurationNanoseconds):F1}");
        output.WriteLine(
            $"MODEL_ROLE_SUMMARY|" +
            $"model={responseModel}|" +
            "roleFidelity=true|" +
            $"ttftMs={firstToken.TimeToFirstTokenMilliseconds:F1}|" +
            $"promptTokens={completedTelemetry.PromptTokenCount}|" +
            $"outputTokens={completedTelemetry.OutputTokenCount}|" +
            $"totalMs=" +
            $"{LocalModelEvaluationSupport.NanosecondsToMilliseconds(completedTelemetry.TotalDurationNanoseconds):F1}");
    }

    private static AgentSettingsSnapshot CreateCustomAgentSettings(
        string model) =>
        new(
            CustomAgentId,
            "argument-analyst",
            "Argument Analyst",
            "🧭",
            "#64748b",
            model,
            $"""
            You are an evaluation-only Argument Analyst.
            Every response MUST begin exactly with "{RolePrefix}".
            Analyse logical structure, premises, conclusions and validity.
            Do not act as a historian or confessional advocate.
            Never reproduce unrelated text from another specialist's history.
            Answer the current question concisely in French.
            """,
            "Analyse logical structure and validity without confessional advocacy.",
            IsBuiltIn: false,
            IsEnabled: true,
            UpdatedAt: DateTimeOffset.UtcNow);

    private static AiRuntimeSettingsSnapshot CreateRuntimeSettings(
        string responseModel) =>
        new(
            AiRuntimeSettingsSnapshot.OllamaProvider,
            LocalModelEvaluationSupport.GetBaseAddress().ToString(),
            LocalModelEvaluationSupport.GetRoutingModel(),
            responseModel,
            RoutingTimeoutSeconds: 60,
            GenerationTimeoutSeconds: 180,
            KeepAlive: "10m",
            MaximumHistoryMessages: 10,
            MaximumHistoryCharacters: 10_000,
            MaximumOutputTokens: 300,
            UpdatedAt: DateTimeOffset.UtcNow,
            AgentModels:
                new Dictionary<Guid, string>());

    private static AgentTurnRequest CreateRequest()
    {
        var previousUserMessageId = MessageId.New();
        var previousAgentMessageId = MessageId.New();
        var currentMessageId = MessageId.New();

        return new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            currentMessageId,
            RequestedAgentId: CustomAgentId,
            History:
            [
                new ConversationMessageContext(
                    previousUserMessageId,
                    MessageRole.User,
                    "Défends la Trinité.",
                    AgentId: null,
                    DateTimeOffset.UtcNow.AddMinutes(-2)),
                new ConversationMessageContext(
                    previousAgentMessageId,
                    MessageRole.Agent,
                    $"Réponse apologétique {OtherAgentHistoryCanary}",
                    BuiltInAgents.ProtestantApologist.Id,
                    DateTimeOffset.UtcNow.AddMinutes(-1)),
                new ConversationMessageContext(
                    currentMessageId,
                    MessageRole.User,
                    "Analyse en une phrase si cette conclusion suit des prémisses : tous les hommes sont mortels ; Socrate est un homme ; donc Socrate est mortel.",
                    AgentId: null,
                    DateTimeOffset.UtcNow)
            ]);
    }

    private sealed class UnusedAgentRouter
        : IAgentRouter
    {
        public ValueTask<RoutingDecision> RouteAsync(
            AgentTurnRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The routed runtime overload should be used by this evaluation.");
    }
}
