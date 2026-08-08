using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Infrastructure.Persistence.AiRuntime;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Repositories;

public sealed class EfAiRuntimeSettingsStore(
    ApologiaStudioDbContext dbContext)
    : IAiRuntimeSettingsStore
{
    public async Task<AiRuntimeSettingsSnapshot?> GetAsync(
        CancellationToken cancellationToken)
    {
        var entity =
            await dbContext.AiRuntimeSettings
                .AsNoTracking()
                .Include(settings => settings.AgentModels)
                .SingleOrDefaultAsync(
                    settings =>
                        settings.Provider ==
                        AiRuntimeSettingsSnapshot.OllamaProvider,
                    cancellationToken);

        return entity is null
            ? null
            : Map(entity);
    }

    public async Task SaveAsync(
        AiRuntimeSettingsSnapshot settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var entity =
            await dbContext.AiRuntimeSettings
                .Include(candidate => candidate.AgentModels)
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.Provider == settings.Provider,
                    cancellationToken);

        if (entity is null)
        {
            entity =
                new AiRuntimeSettingsEntity
                {
                    Provider = settings.Provider
                };

            dbContext.AiRuntimeSettings.Add(entity);
        }

        entity.BaseAddress = settings.BaseAddress;
        entity.RoutingModel = settings.RoutingModel;
        entity.DefaultAgentModel = settings.DefaultAgentModel;
        entity.RoutingTimeoutSeconds = settings.RoutingTimeoutSeconds;
        entity.GenerationTimeoutSeconds = settings.GenerationTimeoutSeconds;
        entity.KeepAlive = settings.KeepAlive;
        entity.MaximumHistoryMessages = settings.MaximumHistoryMessages;
        entity.MaximumHistoryCharacters = settings.MaximumHistoryCharacters;
        entity.MaximumOutputTokens = settings.MaximumOutputTokens;
        entity.UpdatedAt = settings.UpdatedAt;

        var desiredAssignments =
            settings.AgentModels.ToDictionary(
                assignment => assignment.Key,
                assignment => assignment.Value);

        foreach (var assignment in entity.AgentModels.ToArray())
        {
            if (desiredAssignments.Remove(assignment.AgentId, out var model))
            {
                assignment.Model = model;
                continue;
            }

            dbContext.AiAgentModelAssignments.Remove(assignment);
        }

        foreach (var assignment in desiredAssignments)
        {
            entity.AgentModels.Add(
                new AiAgentModelAssignmentEntity
                {
                    Provider = settings.Provider,
                    AgentId = assignment.Key,
                    Model = assignment.Value
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AiRuntimeSettingsSnapshot Map(
        AiRuntimeSettingsEntity entity)
    {
        return new AiRuntimeSettingsSnapshot(
            entity.Provider,
            entity.BaseAddress,
            entity.RoutingModel,
            entity.DefaultAgentModel,
            entity.RoutingTimeoutSeconds,
            entity.GenerationTimeoutSeconds,
            entity.KeepAlive,
            entity.MaximumHistoryMessages,
            entity.MaximumHistoryCharacters,
            entity.MaximumOutputTokens,
            entity.UpdatedAt,
            entity.AgentModels.ToDictionary(
                assignment => assignment.AgentId,
                assignment => assignment.Model));
    }
}
