using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlAgentSettingsStoreTests
{
    [Fact]
    public async Task Store_ShouldEnforceActiveLimitAndKeepDeactivatedAgent()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_TEST_DB_CONNECTION");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        var options =
            new DbContextOptionsBuilder<ApologiaStudioDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await using (
            var initializationContext =
                new ApologiaStudioDbContext(options))
        {
            await initializationContext.Database.EnsureDeletedAsync();
            await initializationContext.Database.MigrateAsync();
        }

        await using var context =
            new ApologiaStudioDbContext(options);
        var store = new EfAgentSettingsStore(context);
        var createdAgents = new List<AgentSettingsSnapshot>();

        for (var index = 0;
             index < AgentSettingsPolicy.MaximumActiveAgents;
             index++)
        {
            var settings = CreateSettings(index);
            Assert.True(
                await store.TryCreateAsync(
                    settings,
                    AgentSettingsPolicy.MaximumActiveAgents,
                    CancellationToken.None));
            createdAgents.Add(settings);
        }

        Assert.False(
            await store.TryCreateAsync(
                CreateSettings(99),
                AgentSettingsPolicy.MaximumActiveAgents,
                CancellationToken.None));

        Assert.True(
            await store.DeactivateAsync(
                createdAgents[0].AgentId,
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        Assert.True(
            await store.TryCreateAsync(
                CreateSettings(100),
                AgentSettingsPolicy.MaximumActiveAgents,
                CancellationToken.None));

        var all = await store.ListAsync(CancellationToken.None);

        Assert.Equal(
            AgentSettingsPolicy.MaximumActiveAgents,
            all.Count(settings => settings.IsEnabled));
        Assert.Contains(
            all,
            settings =>
                settings.AgentId == createdAgents[0].AgentId &&
                !settings.IsEnabled);
    }

    private static AgentSettingsSnapshot CreateSettings(int index)
    {
        var agentId = AgentId.New();
        return new AgentSettingsSnapshot(
            agentId,
            $"custom-{agentId.Value:N}",
            $"Agent {index}",
            "AI",
            "#AABBCC",
            null,
            "System prompt",
            $"Routing description {index}",
            IsBuiltIn: false,
            IsEnabled: true,
            UpdatedAt: DateTimeOffset.UtcNow);
    }
}
