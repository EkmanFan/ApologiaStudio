using System.Data;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Infrastructure.Persistence.AiRuntime;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Repositories;

public sealed class EfAgentSettingsStore(
    ApologiaStudioDbContext dbContext)
    : IAgentSettingsStore
{
    public async Task<IReadOnlyList<AgentSettingsSnapshot>> ListAsync(
        CancellationToken cancellationToken)
    {
        var entities = await dbContext.AiAgentSettings
            .AsNoTracking()
            .OrderByDescending(settings => settings.IsBuiltIn)
            .ThenBy(settings => settings.DisplayName)
            .ToArrayAsync(cancellationToken);

        return entities
            .Select(Map)
            .ToArray();
    }

    public async Task<AgentSettingsSnapshot?> GetAsync(
        AgentId agentId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.AiAgentSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                settings => settings.AgentId == agentId.Value,
                cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<bool> TryCreateAsync(
        AgentSettingsSnapshot settings,
        int maximumActiveAgents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (maximumActiveAgents <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumActiveAgents));
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var activeAgentCount = await dbContext.AiAgentSettings
            .CountAsync(
                candidate => candidate.IsEnabled,
                cancellationToken);
        if (activeAgentCount >= maximumActiveAgents)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        dbContext.AiAgentSettings.Add(CreateEntity(settings));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task SaveAsync(
        AgentSettingsSnapshot settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var entity = await dbContext.AiAgentSettings
            .SingleOrDefaultAsync(
                candidate => candidate.AgentId == settings.AgentId.Value,
                cancellationToken);
        if (entity is null)
        {
            entity = new AiAgentSettingsEntity
            {
                AgentId = settings.AgentId.Value
            };
            dbContext.AiAgentSettings.Add(entity);
        }

        Apply(entity, settings);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeactivateAsync(
        AgentId agentId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.AiAgentSettings
            .SingleOrDefaultAsync(
                candidate => candidate.AgentId == agentId.Value,
                cancellationToken);
        if (entity is null || entity.IsBuiltIn)
        {
            return false;
        }

        entity.IsEnabled = false;
        entity.UpdatedAt = updatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static AiAgentSettingsEntity CreateEntity(
        AgentSettingsSnapshot settings)
    {
        var entity = new AiAgentSettingsEntity
        {
            AgentId = settings.AgentId.Value
        };
        Apply(entity, settings);
        return entity;
    }

    private static void Apply(
        AiAgentSettingsEntity entity,
        AgentSettingsSnapshot settings)
    {
        entity.Slug = settings.Slug;
        entity.DisplayName = settings.DisplayName;
        entity.Avatar = settings.Avatar;
        entity.BubbleColor = settings.BubbleColor;
        entity.Model = settings.Model;
        entity.SystemPrompt = settings.SystemPrompt;
        entity.RoutingDescription = settings.RoutingDescription;
        entity.IsBuiltIn = settings.IsBuiltIn;
        entity.IsEnabled = settings.IsEnabled;
        entity.UpdatedAt = settings.UpdatedAt;
    }

    private static AgentSettingsSnapshot Map(AiAgentSettingsEntity entity)
    {
        return new AgentSettingsSnapshot(
            new AgentId(entity.AgentId),
            entity.Slug ?? string.Empty,
            entity.DisplayName,
            entity.Avatar,
            entity.BubbleColor,
            entity.Model,
            entity.SystemPrompt,
            entity.RoutingDescription ?? string.Empty,
            entity.IsBuiltIn,
            entity.IsEnabled,
            entity.UpdatedAt);
    }
}
