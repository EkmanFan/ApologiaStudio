using ApologiaStudio.Application.AiRuntime.Settings;

namespace ApologiaStudio.UnitTests.Application.AiRuntime;

public sealed class AiRuntimeSettingsValidatorTests
{
    [Fact]
    public void Normalize_ShouldRejectNonLoopbackAddress()
    {
        var command =
            CreateCommand(
                baseAddress: "https://example.com");

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    AiRuntimeSettingsValidator.Normalize(
                        command,
                        DateTimeOffset.UtcNow));

        Assert.Contains(
            "loopback",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_ShouldNormalizeModelsAndAssignments()
    {
        var agentId = Guid.NewGuid();

        var command =
            CreateCommand(
                routingModel: " qwen3:8b ",
                defaultAgentModel: " mixtral:instruct ",
                assignments:
                [
                    new AgentModelAssignmentInput(
                        agentId,
                        " qwen2.5vl:32b ")
                ]);

        var settings =
            AiRuntimeSettingsValidator.Normalize(
                command,
                DateTimeOffset.UtcNow);

        Assert.Equal(
            "http://127.0.0.1:11434/",
            settings.BaseAddress);
        Assert.Equal("qwen3:8b", settings.RoutingModel);
        Assert.Equal(
            "mixtral:instruct",
            settings.DefaultAgentModel);
        Assert.Equal(
            "qwen2.5vl:32b",
            settings.AgentModels[agentId]);
    }

    [Fact]
    public void Normalize_ShouldRejectDuplicateAgentAssignments()
    {
        var agentId = Guid.NewGuid();

        var command =
            CreateCommand(
                assignments:
                [
                    new AgentModelAssignmentInput(
                        agentId,
                        "qwen3:8b"),
                    new AgentModelAssignmentInput(
                        agentId,
                        "mixtral:instruct")
                ]);

        Assert.Throws<ArgumentException>(
            () =>
                AiRuntimeSettingsValidator.Normalize(
                    command,
                    DateTimeOffset.UtcNow));
    }

    private static UpdateAiRuntimeSettingsCommand CreateCommand(
        string baseAddress = "http://127.0.0.1:11434",
        string routingModel = "qwen3:8b",
        string defaultAgentModel = "qwen3:8b",
        IReadOnlyList<AgentModelAssignmentInput>? assignments = null)
    {
        return new UpdateAiRuntimeSettingsCommand(
            baseAddress,
            routingModel,
            defaultAgentModel,
            RoutingTimeoutSeconds: 60,
            GenerationTimeoutSeconds: 180,
            KeepAlive: "10m",
            MaximumHistoryMessages: 24,
            MaximumHistoryCharacters: 24_000,
            MaximumOutputTokens: 1_200,
            AgentModels:
                assignments ??
                Array.Empty<AgentModelAssignmentInput>());
    }
}
