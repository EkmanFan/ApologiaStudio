using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Persistence;

namespace ApologiaStudio.Application.Navigation.ReorderPinnedItems;

public sealed class ReorderPinnedItemsHandler(
    ISidebarPinRepository pinRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(
        ReorderPinnedItemsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.OrderedPinIds);

        var pins = await pinRepository.ListByOwnerAsync(
            currentUser.UserId,
            cancellationToken);

        var pinById = pins.ToDictionary(pin => pin.Id);

        if (command.OrderedPinIds.Count != pins.Count ||
            command.OrderedPinIds.Distinct().Count() != pins.Count ||
            command.OrderedPinIds.Any(id => !pinById.ContainsKey(id)))
        {
            throw new ArgumentException(
                "The pinned order must contain every owned shortcut exactly once.",
                nameof(command));
        }

        for (var index = 0;
             index < command.OrderedPinIds.Count;
             index++)
        {
            pinById[command.OrderedPinIds[index]].Reorder(index);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
