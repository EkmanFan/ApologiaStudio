using ApologiaStudio.Domain.Navigation;

namespace ApologiaStudio.Application.Navigation.ReorderPinnedItems;

public sealed record ReorderPinnedItemsCommand(
    IReadOnlyList<SidebarPinId> OrderedPinIds);
