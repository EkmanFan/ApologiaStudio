using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.UnitTests.Application.Agents;

public sealed class InitializeAgentSettingsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldCreateOnlyMissingAgentSettings()
    {
        var existingId = new AgentId(Guid.NewGuid());
        var missingId = new AgentId(Guid.NewGuid());
        var existing = CreateSettings(existingId, "Existing");
        var store = new FakeAgentSettingsStore(existing);
        var handler = new InitializeAgentSettingsHandler(store);

        var created = await handler.HandleAsync(
            [
                CreateSettings(existingId, "Should not overwrite"),
                CreateSettings(missingId, "Missing")
            ],
            CancellationToken.None);

        Assert.Equal(1, created);
        Assert.Equal("Existing", (await store.GetAsync(
            existingId,
            CancellationToken.None))!.DisplayName);
        Assert.Equal("Missing", (await store.GetAsync(
            missingId,
            CancellationToken.None))!.DisplayName);
    }

    private static AgentSettingsSnapshot CreateSettings(
        AgentId agentId,
        string displayName)
    {
        return new AgentSettingsSnapshot(
            agentId,
            displayName,
            "AI",
            "#FFFFFF",
            null,
            "Prompt",
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeAgentSettingsStore(
        params AgentSettingsSnapshot[] initialSettings)
        : IAgentSettingsStore
    {
        private readonly Dictionary<AgentId, AgentSettingsSnapshot> _settings =
            initialSettings.ToDictionary(item => item.AgentId);

        public Task<IReadOnlyList<AgentSettingsSnapshot>> ListAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AgentSettingsSnapshot> result =
                _settings.Values.ToArray();
            return Task.FromResult(result);
        }

        public Task<AgentSettingsSnapshot?> GetAsync(
            AgentId agentId,
            CancellationToken cancellationToken)
        {
            _settings.TryGetValue(agentId, out var settings);
            return Task.FromResult(settings);
        }

        public Task SaveAsync(
            AgentSettingsSnapshot settings,
            CancellationToken cancellationToken)
        {
            _settings[settings.AgentId] = settings;
            return Task.CompletedTask;
        }
    }
}
