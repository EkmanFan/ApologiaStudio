using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.UnitTests.Application.Agents;

public sealed class AgentSettingsValidatorTests
{
    private static readonly AgentId AgentId = new(Guid.NewGuid());

    [Fact]
    public void Normalize_ShouldTrimValuesAndAllowDefaultModel()
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var result = AgentSettingsValidator.Normalize(
            new UpdateAgentSettingsCommand(
                AgentId,
                "  Historian  ",
                " 🏛️ ",
                "#e7eef4",
                "   ",
                "  System prompt  "),
            updatedAt);

        Assert.Equal("Historian", result.DisplayName);
        Assert.Equal("🏛️", result.Avatar);
        Assert.Equal("#E7EEF4", result.BubbleColor);
        Assert.Null(result.Model);
        Assert.Equal("System prompt", result.SystemPrompt);
        Assert.Equal(updatedAt, result.UpdatedAt);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("")]
    public void Normalize_ShouldRejectInvalidBubbleColor(string color)
    {
        var command = new UpdateAgentSettingsCommand(
            AgentId,
            "Agent",
            "A",
            color,
            null,
            "Prompt");

        Assert.Throws<ArgumentException>(
            () => AgentSettingsValidator.Normalize(
                command,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Normalize_ShouldRejectEmptyPrompt()
    {
        var command = new UpdateAgentSettingsCommand(
            AgentId,
            "Agent",
            "A",
            "#FFFFFF",
            null,
            "   ");

        Assert.Throws<ArgumentException>(
            () => AgentSettingsValidator.Normalize(
                command,
                DateTimeOffset.UtcNow));
    }
}
