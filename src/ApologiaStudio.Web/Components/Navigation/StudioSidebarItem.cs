namespace ApologiaStudio.Web.Components.Navigation;

public sealed record StudioSidebarConversation(
    Guid Id,
    Guid? ProjectId,
    string Url,
    string Title,
    bool IsActive,
    bool IsPinned)
{
    public StudioSidebarConversation(
        string url,
        string title,
        bool isActive)
        : this(
            Guid.Empty,
            null,
            url,
            title,
            isActive,
            false)
    {
    }
}

public sealed record StudioSidebarPinnedItem(
    Guid PinId,
    Guid TargetId,
    string Url,
    string Title,
    bool IsProject,
    bool IsActive)
{
    public StudioSidebarPinnedItem(
        string url,
        string title,
        bool isProject,
        bool isActive)
        : this(
            Guid.Empty,
            Guid.Empty,
            url,
            title,
            isProject,
            isActive)
    {
    }
}

public sealed record StudioSidebarProject(
    Guid Id,
    string AnchorId,
    string Name,
    bool IsPinned,
    IReadOnlyList<StudioSidebarConversation> Conversations)
{
    public StudioSidebarProject(
        string anchorId,
        string name,
        IReadOnlyList<StudioSidebarConversation> conversations)
        : this(
            Guid.Empty,
            anchorId,
            name,
            false,
            conversations)
    {
    }
}

public sealed record StudioSidebarDeletedConversation(
    Guid Id,
    string Title,
    DateTimeOffset DeletedAt);

public sealed record StudioSidebarBibleEdition(
    string Code,
    string DisplayName,
    string LanguageTag,
    string Url,
    bool IsActive)
{
    public StudioSidebarBibleEdition(
        string code,
        string displayName,
        string languageTag)
        : this(
            code,
            displayName,
            languageTag,
            $"/library/{Uri.EscapeDataString(code)}",
            false)
    {
    }
}

public sealed record StudioSidebarRenameRequest(
    Guid TargetId,
    bool IsProject,
    string Name);

public sealed record StudioSidebarPinRequest(
    Guid TargetId,
    bool IsProject,
    bool IsPinned);

public sealed record StudioSidebarMoveConversationRequest(
    Guid ConversationId,
    Guid? ProjectId,
    int Position);

public sealed record StudioSidebarDeleteRequest(
    Guid TargetId,
    bool IsProject);
