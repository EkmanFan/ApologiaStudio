using ApologiaStudio.Application.Abstractions.Persistence;

namespace ApologiaStudio.Infrastructure.Persistence;

public sealed class EfUnitOfWork(
    ApologiaStudioDbContext dbContext)
    : IUnitOfWork
{
    public Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
