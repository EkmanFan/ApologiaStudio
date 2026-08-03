using ApologiaStudio.Domain.Navigation;

namespace ApologiaStudio.Application.Navigation.SetSidebarPin;

public sealed record SetSidebarPinCommand(
    SidebarPinTargetKind TargetKind,
    Guid TargetId,
    bool IsPinned);
