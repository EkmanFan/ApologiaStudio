namespace ApologiaStudio.Domain.Navigation;

public readonly record struct SidebarPinId(Guid Value)
{
    public static SidebarPinId New() =>
        new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
