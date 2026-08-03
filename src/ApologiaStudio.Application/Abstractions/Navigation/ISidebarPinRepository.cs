using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Abstractions.Navigation;

public interface ISidebarPinRepository
{
    Task<IReadOnlyList<SidebarPin>> ListByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken);

    void Add(SidebarPin pin);
}
