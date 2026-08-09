using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.UnitTests.Application.Agents;

public sealed class AgentSettingsValidatorTests
{
    private static readonly AgentId AgentId = new(Guid.NewGuid());

    [Fact]
    public void NormalizeUpdate_ShouldTrimValuesAndAllowDefaultModel()
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var existing = CreateSettings();
        var result = AgentSettingsValidator.NormalizeUpdate(
            new UpdateAgentSettingsCommand(
                AgentId,
                "  Historian  ",
                " 🏛️ ",
                "#e7eef4",
                "   ",
                "  System prompt  ",
                "  Historical questions and chronology.  "),
            existing,
            updatedAt);

        Assert.Equal("Historian", result.DisplayName);
        Assert.Equal("🏛️", result.Avatar);
        Assert.Equal("#E7EEF4", result.BubbleColor);
        Assert.Null(result.Model);
        Assert.Equal("System prompt", result.SystemPrompt);
        Assert.Equal(
            "Historical questions and chronology.",
            result.RoutingDescription);
        Assert.Equal(existing.Slug, result.Slug);
        Assert.True(result.IsBuiltIn);
        Assert.Equal(updatedAt, result.UpdatedAt);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("")]
    public void NormalizeUpdate_ShouldRejectInvalidBubbleColor(string color)
    {
        var command = new UpdateAgentSettingsCommand(
            AgentId,
            "Agent",
            "A",
            color,
            null,
            "Prompt",
            "Routing description");

        Assert.Throws<ArgumentException>(
            () => AgentSettingsValidator.NormalizeUpdate(
                command,
                CreateSettings(),
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NormalizeCreate_ShouldRejectEmptyRoutingDescription()
    {
        var command = new CreateAgentSettingsCommand(
            "Agent",
            "A",
            "#FFFFFF",
            null,
            "Prompt",
            "   ");

        Assert.Throws<ArgumentException>(
            () => AgentSettingsValidator.NormalizeCreate(
                AgentId,
                "custom-test",
                command,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NormalizeCreate_ShouldMarkCustomAgentEnabledAndDeletable()
    {
        var result = AgentSettingsValidator.NormalizeCreate(
            AgentId,
            "custom-test",
            new CreateAgentSettingsCommand(
                "Agent",
                "🤖",
                "#AABBCC",
                "qwen3:8b",
                "System prompt",
                "Specialized test questions."),
            DateTimeOffset.UtcNow);

        Assert.Equal("custom-test", result.Slug);
        Assert.False(result.IsBuiltIn);
        Assert.True(result.IsEnabled);
    }

    private static AgentSettingsSnapshot CreateSettings()
    {
        return new AgentSettingsSnapshot(
            AgentId,
            "historian",
            "Historian",
            "🏛️",
            "#E7EEF4",
            null,
            "Prompt",
            "Historical questions.",
            IsBuiltIn: true,
            IsEnabled: true,
            UpdatedAt: DateTimeOffset.UtcNow);
    }
}
