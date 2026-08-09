using ApologiaStudio.AgentRuntime.Agents;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;

namespace ApologiaStudio.UnitTests.AgentRuntime.Routing;

public sealed class DatabaseAgentRoutingSnapshotProviderTests
{
    private static readonly AgentDescriptor CustomAgent = new(
        new AgentId(
            Guid.Parse("44444444-4444-4444-4444-444444444444")),
        "custom-specialist",
        "Custom Specialist");

    [Fact]
    public async Task GetActiveAsync_ShouldReadFreshSettingsOnEveryCall()
    {
        var customSettings = CreateSettings(
            new AgentRoutingProfile(
                CustomAgent,
                "- custom specialist requests;"),
            isBuiltIn: false,
            isEnabled: false);
        var store = new MutableAgentSettingsStore(
            CreateBuiltInSettings()
                .Append(customSettings)
                .ToArray());
        var provider = new DatabaseAgentRoutingSnapshotProvider(store);

        var first = await provider.GetActiveAsync(
            CancellationToken.None);

        Assert.Equal(2, first.All.Count);
        Assert.False(first.TryGet(CustomAgent.Id, out _));

        store.Settings = CreateBuiltInSettings()
            .Append(customSettings with
            {
                IsEnabled = true,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .ToArray();

        var second = await provider.GetActiveAsync(
            CancellationToken.None);

        Assert.Equal(3, second.All.Count);
        Assert.True(second.TryGet(CustomAgent.Id, out var custom));
        Assert.Equal(CustomAgent, custom.Agent);
        Assert.Equal(2, store.ListCallCount);
    }

    [Fact]
    public async Task GetActiveAsync_ShouldRejectMissingBuiltInAgent()
    {
        var settings = CreateBuiltInSettings()
            .Where(
                candidate =>
                    candidate.AgentId ==
                    BuiltInAgents.ProtestantApologist.Id)
            .ToArray();
        var provider = new DatabaseAgentRoutingSnapshotProvider(
            new MutableAgentSettingsStore(settings));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetActiveAsync(
                CancellationToken.None));

        Assert.Contains(
            BuiltInAgents.Historian.Slug,
            exception.Message,
            StringComparison.Ordinal);
    }

    private static IEnumerable<AgentSettingsSnapshot>
        CreateBuiltInSettings()
    {
        return BuiltInAgentRegistry.Profiles.Select(
            profile => CreateSettings(
                profile,
                isBuiltIn: true,
                isEnabled: true));
    }

    private static AgentSettingsSnapshot CreateSettings(
        AgentRoutingProfile profile,
        bool isBuiltIn,
        bool isEnabled)
    {
        return new AgentSettingsSnapshot(
            profile.Agent.Id,
            profile.Agent.Slug,
            profile.Agent.DisplayName,
            "🤖",
            "#EAF0F3",
            Model: null,
            SystemPrompt: "System prompt",
            RoutingDescription: profile.RoutingDescription,
            IsBuiltIn: isBuiltIn,
            IsEnabled: isEnabled,
            UpdatedAt: DateTimeOffset.UtcNow);
    }

    private sealed class MutableAgentSettingsStore(
        IReadOnlyList<AgentSettingsSnapshot> settings)
        : IAgentSettingsStore
    {
        public IReadOnlyList<AgentSettingsSnapshot> Settings { get; set; } =
            settings;

        public int ListCallCount { get; private set; }

        public Task<IReadOnlyList<AgentSettingsSnapshot>> ListAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListCallCount++;
            return Task.FromResult(Settings);
        }

        public Task<AgentSettingsSnapshot?> GetAsync(
            AgentId agentId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                Settings.SingleOrDefault(
                    candidate => candidate.AgentId == agentId));
        }

        public Task<bool> TryCreateAsync(
            AgentSettingsSnapshot settings,
            int maximumActiveAgents,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync(
            AgentSettingsSnapshot settings,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeactivateAsync(
            AgentId agentId,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
