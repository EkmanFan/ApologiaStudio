namespace ApologiaStudio.Web.Components.Navigation;

public sealed record StudioSidebarConversation(
    string Url,
    string Title,
    bool IsActive);

public sealed record StudioSidebarPinnedItem(
    string Url,
    string Title,
    bool IsProject,
    bool IsActive);

public sealed record StudioSidebarProject(
    string AnchorId,
    string Name,
    IReadOnlyList<StudioSidebarConversation> Conversations);

public sealed record StudioSidebarBibleEdition(
    string Code,
    string DisplayName,
    string LanguageTag);
