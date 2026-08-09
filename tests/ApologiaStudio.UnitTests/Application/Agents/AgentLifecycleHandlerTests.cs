using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.UnitTests.Application.Agents;

public sealed class AgentLifecycleHandlerTests
{
    [Fact]
    public async Task Create_ShouldCreateCustomAgentBelowMaximum()
    {
        var store = new FakeAgentSettingsStore(
            Enumerable.Range(0, 2)
                .Select(index => CreateSettings(
                    new AgentId(Guid.NewGuid()),
                    $"built-in-{index}",
                    isBuiltIn: true))
                .ToArray());
        var handler = new CreateAgentSettingsHandler(
            store,
            TimeProvider.System);

        var created = await handler.HandleAsync(
            CreateCommand(),
            CancellationToken.None);

        Assert.False(created.IsBuiltIn);
        Assert.True(created.IsEnabled);
        Assert.StartsWith("custom-", created.Slug);
        Assert.Equal(3, (await store.ListAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Create_ShouldRejectNinthActiveAgent()
    {
        var store = new FakeAgentSettingsStore(
            Enumerable.Range(0, AgentSettingsPolicy.MaximumActiveAgents)
                .Select(index => CreateSettings(
                    new AgentId(Guid.NewGuid()),
                    $"agent-{index}",
                    isBuiltIn: index < 2))
                .ToArray());
        var handler = new CreateAgentSettingsHandler(
            store,
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                CreateCommand(),
                CancellationToken.None));

        Assert.Contains("8", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            AgentSettingsPolicy.MaximumActiveAgents,
            (await store.ListAsync(CancellationToken.None))
                .Count(settings => settings.IsEnabled));
    }

    [Fact]
    public async Task Delete_ShouldRejectBuiltInAgent()
    {
        var builtIn = CreateSettings(
            new AgentId(Guid.NewGuid()),
            "historian",
            isBuiltIn: true);
        var store = new FakeAgentSettingsStore(builtIn);
        var handler = new DeleteAgentSettingsHandler(
            store,
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                builtIn.AgentId,
                CancellationToken.None));

        Assert.True(
            (await store.GetAsync(
                builtIn.AgentId,
                CancellationToken.None))!.IsEnabled);
    }

    [Fact]
    public async Task Delete_ShouldDeactivateCustomAgent()
    {
        var custom = CreateSettings(
            new AgentId(Guid.NewGuid()),
            "custom-test",
            isBuiltIn: false);
        var store = new FakeAgentSettingsStore(custom);
        var handler = new DeleteAgentSettingsHandler(
            store,
            TimeProvider.System);

        await handler.HandleAsync(
            custom.AgentId,
            CancellationToken.None);

        Assert.False(
            (await store.GetAsync(
                custom.AgentId,
                CancellationToken.None))!.IsEnabled);
    }

    private static CreateAgentSettingsCommand CreateCommand()
    {
        return new CreateAgentSettingsCommand(
            "Custom specialist",
            "🤖",
            "#AABBCC",
            null,
            "You are a specialized assistant.",
            "Specialized user questions for this domain.");
    }

    private static AgentSettingsSnapshot CreateSettings(
        AgentId agentId,
        string slug,
        bool isBuiltIn)
    {
        return new AgentSettingsSnapshot(
            agentId,
            slug,
            slug,
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
            initialSettings.ToDictionary(settings => settings.AgentId);

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
            _settings.TryGetValue(agentId, out var result);
            return Task.FromResult(result);
        }

        public Task<bool> TryCreateAsync(
            AgentSettingsSnapshot settings,
            int maximumActiveAgents,
            CancellationToken cancellationToken)
        {
            if (_settings.Values.Count(item => item.IsEnabled) >=
                maximumActiveAgents)
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
