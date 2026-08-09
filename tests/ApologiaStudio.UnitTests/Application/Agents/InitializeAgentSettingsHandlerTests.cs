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
        var existing = CreateSettings(
            existingId,
            "historian",
            "Existing",
            isBuiltIn: true);
        var store = new FakeAgentSettingsStore(existing);
        var handler = new InitializeAgentSettingsHandler(store);

        var created = await handler.HandleAsync(
            [
                CreateSettings(
                    existingId,
                    "historian",
                    "Should not overwrite",
                    isBuiltIn: true),
                CreateSettings(
                    missingId,
                    "missing",
                    "Missing",
                    isBuiltIn: true)
            ],
            CancellationToken.None);

        Assert.Equal(1, created);
        Assert.Equal(
            "Existing",
            (await store.GetAsync(
                existingId,
                CancellationToken.None))!.DisplayName);
        Assert.Equal(
            "Missing",
            (await store.GetAsync(
                missingId,
                CancellationToken.None))!.DisplayName);
    }

    [Fact]
    public async Task HandleAsync_ShouldRepairBuiltInRoutingMetadataWithoutOverwritingPrompt()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var existing = CreateSettings(
            agentId,
            string.Empty,
            "Custom display",
            isBuiltIn: false) with
        {
            SystemPrompt = "User edited prompt",
            RoutingDescription = string.Empty,
            IsEnabled = false
        };
        var store = new FakeAgentSettingsStore(existing);
        var handler = new InitializeAgentSettingsHandler(store);

        await handler.HandleAsync(
            [
                CreateSettings(
                    agentId,
                    "historian",
                    "Default display",
                    isBuiltIn: true)
            ],
            CancellationToken.None);

        var repaired = await store.GetAsync(
            agentId,
            CancellationToken.None);

        Assert.NotNull(repaired);
        Assert.Equal("Custom display", repaired.DisplayName);
        Assert.Equal("User edited prompt", repaired.SystemPrompt);
        Assert.Equal("historian", repaired.Slug);
        Assert.Equal("Routing for historian", repaired.RoutingDescription);
        Assert.True(repaired.IsBuiltIn);
        Assert.True(repaired.IsEnabled);
    }

    [Fact]
    public async Task HandleAsync_ShouldPreserveUserEditedBuiltInRoutingDescription()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var existing = CreateSettings(
            agentId,
            "historian",
            "Historian",
            isBuiltIn: true) with
        {
            RoutingDescription = "User edited routing description"
        };
        var store = new FakeAgentSettingsStore(existing);
        var handler = new InitializeAgentSettingsHandler(store);

        await handler.HandleAsync(
            [
                CreateSettings(
                    agentId,
                    "historian",
                    "Historian",
                    isBuiltIn: true)
            ],
            CancellationToken.None);

        var result = await store.GetAsync(
            agentId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            "User edited routing description",
            result.RoutingDescription);
    }

    private static AgentSettingsSnapshot CreateSettings(
        AgentId agentId,
        string slug,
        string displayName,
        bool isBuiltIn)
    {
        return new AgentSettingsSnapshot(
            agentId,
            slug,
            displayName,
            "AI",
            "#FFFFFF",
            null,
            "Prompt",
            $"Routing for {slug}",
            isBuiltIn,
            IsEnabled: true,
            UpdatedAt: DateTimeOffset.UtcNow);
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

        public Task<bool> TryCreateAsync(
            AgentSettingsSnapshot settings,
            int maximumActiveAgents,
            CancellationToken cancellationToken)
        {
            var activeCount = _settings.Values.Count(item => item.IsEnabled);
            if (activeCount >= maximumActiveAgents)
            {
                return Task.FromResult(false);
            }

            _settings.Add(settings.AgentId, settings);
            return Task.FromResult(true);
        }

        public Task SaveAsync(
            AgentSettingsSnapshot settings,
            CancellationToken cancellationToken)
        {
            _settings[settings.AgentId] = settings;
            return Task.CompletedTask;
        }

        public Task<bool> DeactivateAsync(
            AgentId agentId,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken)
        {
            if (!_settings.TryGetValue(agentId, out var settings) ||
                settings.IsBuiltIn)
            {
                return Task.FromResult(false);
            }

            _settings[agentId] = settings with
            {
                IsEnabled = false,
                UpdatedAt = updatedAt
            };
            return Task.FromResult(true);
        }
    }
}
