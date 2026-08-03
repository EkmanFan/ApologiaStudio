using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Repositories;

public sealed class EfSidebarPinRepository(
    ApologiaStudioDbContext dbContext)
    : ISidebarPinRepository
{
    public async Task<IReadOnlyList<SidebarPin>> ListByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        return await dbContext.SidebarPins
            .Where(pin => pin.OwnerId == ownerId)
            .OrderBy(pin => pin.SortOrder)
            .ThenBy(pin => pin.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(SidebarPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        dbContext.SidebarPins.Add(pin);
    }

    public void Remove(SidebarPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        dbContext.SidebarPins.Remove(pin);
    }
}
