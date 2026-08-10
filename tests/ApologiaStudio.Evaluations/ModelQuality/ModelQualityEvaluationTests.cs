using System.Globalization;
using System.Text;
using System.Text.Json;
using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Agents;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Evaluations.Support;
using Xunit.Abstractions;

namespace ApologiaStudio.Evaluations.ModelQuality;

[Collection(LocalModelEvaluationCollection.Name)]
public sealed class ModelQualityEvaluationTests(
    ITestOutputHelper output)
{
    [Fact]
    public void Dataset_ShouldDefineRepresentativeQualityRubrics()
    {
        var cases = LoadCases();

        Assert.Equal(7, cases.Count);
        Assert.Equal(
            cases.Count,
            cases.Select(candidate => candidate.Id)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.True(
            cases.Count(candidate => candidate.Agent == "historian") >= 5);
        Assert.True(
            cases.Count(candidate => candidate.Agent == "protestant-apologist") >= 2);
        Assert.All(
            cases,
            candidate => Assert.NotEmpty(candidate.RequiredGroups));
        Assert.All(
            cases,
            candidate => Assert.All(
                candidate.RequiredGroups,
                group => Assert.NotEmpty(group)));
    }

    [Trait("Category", "LocalModel")]
    [Trait("Benchmark", "ModelComparison")]
    [Fact]
    public async Task OllamaModel_ShouldRunRepresentativeQualityRubrics_WhenEnabled()
    {
        if (!LocalModelEvaluationSupport.IsEnabled())
        {
            output.WriteLine(
                "Local model evaluation was not enabled.");
            return;
        }

        var model =
            LocalModelEvaluationSupport.GetResponseModel();
        var cases = LoadCases();
        var results = new List<ModelQualityResult>();

        foreach (var evaluationCase in cases)
        {
            var agent = ResolveAgent(evaluationCase.Agent);
            var telemetry =
                new RecordingOllamaRuntimeTelemetry();
            var runtime =
                new OllamaAgentRuntime(
                    new UnusedAgentRouter(),
                    new AgentPromptCatalog(),
                    new EvaluationAiRuntimeSettingsStore(
                        CreateRuntimeSettings(model)),
                    new EvaluationOllamaHttpClientFactory(),
                    telemetry);
            var decision =
                new RoutingDecision(
                    agent.Id,
                    agent.DisplayName,
                    "Evaluation-only explicit routing.",
                    1.0,
                    WasExplicitlyRequested: true);
            AgentTurnCompletedEvent? completedEvent = null;

            await foreach (var runEvent in
                           runtime.RunTurnAsync(
                               CreateRequest(
                                   agent.Id,
                                   evaluationCase.Question),
                               decision,
                               CancellationToken.None))
            {
                if (runEvent is AgentTurnCompletedEvent completed)
                {
                    completedEvent = completed;
                }
            }

            var response =
                Assert.IsType<AgentTurnCompletedEvent>(completedEvent)
                    .Content
                    .Trim();
            Assert.Empty(telemetry.Rejected);
            var firstToken = Assert.Single(telemetry.FirstTokens);
            var completedTelemetry = Assert.Single(telemetry.Completed);
            var result = Score(
                model,
                evaluationCase,
                response,
                firstToken,
                completedTelemetry);
            results.Add(result);

            output.WriteLine(
                $"MODEL_QUALITY_RESPONSE_BEGIN|model={model}|case={evaluationCase.Id}");
            output.WriteLine(response);
            output.WriteLine(
                $"MODEL_QUALITY_RESPONSE_END|model={model}|case={evaluationCase.Id}");
            output.WriteLine(
                $"MODEL_QUALITY_RESULT|" +
                $"model={model}|" +
                $"case={evaluationCase.Id}|" +
                $"agent={evaluationCase.Agent}|" +
                $"coverage={result.Coverage:F3}|" +
                $"forbiddenHits={result.ForbiddenHits}|" +
                $"score={result.Score:F1}|" +
                $"words={result.WordCount}|" +
                $"ttftMs={result.TimeToFirstTokenMilliseconds:F1}|" +
                $"promptTokens={result.PromptTokenCount}|" +
                $"outputTokens={result.OutputTokenCount}|" +
                $"totalMs={result.TotalMilliseconds:F1}");
        }

        output.WriteLine(
            $"MODEL_QUALITY_SUMMARY|" +
            $"model={model}|" +
            $"cases={results.Count}|" +
            $"avgCoverage={results.Average(candidate => candidate.Coverage):F3}|" +
            $"forbiddenHits={results.Sum(candidate => candidate.ForbiddenHits)}|" +
            $"avgScore={results.Average(candidate => candidate.Score):F1}|" +
            $"avgTtftMs={results.Average(candidate => candidate.TimeToFirstTokenMilliseconds):F1}|" +
            $"avgTotalMs={results.Average(candidate => candidate.TotalMilliseconds):F1}|" +
            $"totalOutputTokens={results.Sum(candidate => candidate.OutputTokenCount ?? 0)}");
    }

    private static ModelQualityResult Score(
        string model,
        ModelQualityEvaluationCase evaluationCase,
        string response,
        OllamaGenerationFirstTokenObservation firstToken,
        OllamaGenerationCompletedObservation completed)
    {
        var normalizedResponse = NormalizeForMatch(response);
        var requiredHits =
            evaluationCase.RequiredGroups.Count(
                group => group.Any(
                    candidate => normalizedResponse.Contains(
                        NormalizeForMatch(candidate),
                        StringComparison.Ordinal)));
        var forbiddenHits =
            evaluationCase.ForbiddenPatterns.Count(
                candidate => normalizedResponse.Contains(
                    NormalizeForMatch(candidate),
                    StringComparison.Ordinal));
        var coverage =
            (double)requiredHits /
            evaluationCase.RequiredGroups.Count;
        var score =
            Math.Max(
                0,
                coverage * 100 -
                forbiddenHits * 25);
        var totalMilliseconds =
            LocalModelEvaluationSupport.NanosecondsToMilliseconds(
                completed.TotalDurationNanoseconds) ?? 0;

        return new ModelQualityResult(
            model,
            evaluationCase.Id,
            coverage,
            forbiddenHits,
            score,
            response.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries)
                .Length,
            firstToken.TimeToFirstTokenMilliseconds,
            completed.PromptTokenCount,
            completed.OutputTokenCount,
            totalMilliseconds);
    }

    private static string NormalizeForMatch(
        string value)
    {
        var decomposed =
            value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var normalized = char.ToLowerInvariant(character);
            if (char.IsLetterOrDigit(normalized))
            {
                builder.Append(normalized);
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static AgentDescriptor ResolveAgent(
        string slug) =>
        slug switch
        {
            "historian" => BuiltInAgents.Historian,
            "protestant-apologist" =>
                BuiltInAgents.ProtestantApologist,
            _ => throw new InvalidOperationException(
                $"Unsupported evaluation agent '{slug}'.")
        };

    private static AiRuntimeSettingsSnapshot CreateRuntimeSettings(
        string responseModel) =>
        new(
            AiRuntimeSettingsSnapshot.OllamaProvider,
            LocalModelEvaluationSupport.GetBaseAddress().ToString(),
            LocalModelEvaluationSupport.GetRoutingModel(),
            responseModel,
            RoutingTimeoutSeconds: 60,
            GenerationTimeoutSeconds: 240,
            KeepAlive: "10m",
            MaximumHistoryMessages: 10,
            MaximumHistoryCharacters: 10_000,
            MaximumOutputTokens: 450,
            UpdatedAt: DateTimeOffset.UtcNow,
            AgentModels:
                new Dictionary<Guid, string>());

    private static AgentTurnRequest CreateRequest(
        AgentId agentId,
        string question)
    {
        var messageId = MessageId.New();
        return new AgentTurnRequest(
            ConversationId.New(),
            UserId.New(),
            messageId,
            RequestedAgentId: agentId,
            History:
            [
                new ConversationMessageContext(
                    messageId,
                    MessageRole.User,
                    question,
                    AgentId: null,
                    DateTimeOffset.UtcNow)
            ]);
    }

    private static IReadOnlyList<ModelQualityEvaluationCase>
        LoadCases()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "ModelQuality",
            "model-quality-cases.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<
                List<ModelQualityEvaluationCase>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw new InvalidOperationException(
                "The model-quality evaluation dataset could not be loaded.");
    }

    private sealed record ModelQualityEvaluationCase(
        string Id,
        string Agent,
        string Question,
        List<List<string>> RequiredGroups,
        List<string> ForbiddenPatterns);

    private sealed record ModelQualityResult(
        string Model,
        string CaseId,
        double Coverage,
        int ForbiddenHits,
        double Score,
        int WordCount,
        double TimeToFirstTokenMilliseconds,
        int? PromptTokenCount,
        int? OutputTokenCount,
        double TotalMilliseconds);

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
