namespace ApologiaStudio.Web.Components.Navigation;

public sealed record StudioSidebarConversation(
    string Url,
    string Title,
    bool IsActive);

public sealed record StudioSidebarBibleEdition(
    string Code,
    string DisplayName,
    string LanguageTag);
