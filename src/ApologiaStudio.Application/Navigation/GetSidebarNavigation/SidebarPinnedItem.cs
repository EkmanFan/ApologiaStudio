using ApologiaStudio.Domain.Navigation;

namespace ApologiaStudio.Application.Navigation.GetSidebarNavigation;

public sealed record SidebarPinnedItem(
    SidebarPinId PinId,
    SidebarPinTargetKind TargetKind,
    Guid TargetId,
    string Title,
    int SortOrder);
