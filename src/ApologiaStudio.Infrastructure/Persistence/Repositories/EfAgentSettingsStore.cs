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
            .OrderBy(settings => settings.DisplayName)
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

        entity.DisplayName = settings.DisplayName;
        entity.Avatar = settings.Avatar;
        entity.BubbleColor = settings.BubbleColor;
        entity.Model = settings.Model;
        entity.SystemPrompt = settings.SystemPrompt;
        entity.UpdatedAt = settings.UpdatedAt;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AgentSettingsSnapshot Map(AiAgentSettingsEntity entity)
    {
        return new AgentSettingsSnapshot(
            new AgentId(entity.AgentId),
            entity.DisplayName,
            entity.Avatar,
            entity.BubbleColor,
            entity.Model,
            entity.SystemPrompt,
            entity.UpdatedAt);
    }
}
