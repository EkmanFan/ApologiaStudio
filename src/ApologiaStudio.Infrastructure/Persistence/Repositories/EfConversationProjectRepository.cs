using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Repositories;

public sealed class EfConversationProjectRepository(
    ApologiaStudioDbContext dbContext)
    : IConversationProjectRepository
{
    public async Task<IReadOnlyList<ConversationProject>> ListByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ConversationProjects
            .Where(project => project.OwnerId == ownerId)
            .OrderBy(project => project.SortOrder)
            .ThenBy(project => project.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(ConversationProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        dbContext.ConversationProjects.Add(project);
    }

    public void Remove(ConversationProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        dbContext.ConversationProjects.Remove(project);
    }
}
