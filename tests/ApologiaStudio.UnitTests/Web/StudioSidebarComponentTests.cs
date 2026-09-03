using System.Security.Claims;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.Components.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApologiaStudio.UnitTests.Web;

public sealed class StudioSidebarComponentTests
{
    [Fact]
    public async Task Sidebar_ShouldRenderLibraryBeforeChats()
    {
        var markup = await RenderAsync(
            [
                new StudioSidebarBibleEdition(
                    "lsg1910",
                    "Louis Segond 1910",
                    "fr"),
                new StudioSidebarBibleEdition(
                    "web-classic",
                    "World English Bible Classic",
                    "en")
            ],
            [
                new StudioSidebarConversation(
                    "/conversations/11111111-1111-1111-1111-111111111111",
                    "The resurrection",
                    false)
            ],
            ApplicationLanguage.English);

        var libraryIndex = markup.IndexOf(
            "Library",
            StringComparison.Ordinal);

        var chatsIndex = markup.IndexOf(
            "Chats",
            StringComparison.Ordinal);

        Assert.True(libraryIndex >= 0);
        Assert.True(chatsIndex > libraryIndex);
        Assert.Contains("Louis Segond 1910", markup);
        Assert.Contains("World English Bible Classic", markup);
        Assert.Contains("href=\"/library/lsg1910\"", markup);
        Assert.Contains("href=\"/library/web-classic\"", markup);
        Assert.Contains("The resurrection", markup);
        Assert.DoesNotContain("href=\"/api/bible", markup);
    }

    [Fact]
    public async Task Sidebar_ShouldRenderCleanEmptyStates()
    {
        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            Array.Empty<StudioSidebarConversation>(),
            ApplicationLanguage.English);

        Assert.Contains(
            "No Bible editions are available.",
            markup);

        Assert.Contains(
            "No unassigned chats.",
            markup);
    }

    [Fact]
    public async Task Sidebar_ShouldIdentifyTheActiveConversation()
    {
        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            [
                new StudioSidebarConversation(
                    "/conversations/11111111-1111-1111-1111-111111111111",
                    "First conversation",
                    false),
                new StudioSidebarConversation(
                    "/conversations/22222222-2222-2222-2222-222222222222",
                    "Selected conversation",
                    true)
            ],
            ApplicationLanguage.English);

        Assert.Contains(
            "class=\"conversation-link active\"",
            markup);

        Assert.Contains(
            "aria-current=\"page\"",
            markup);

        Assert.Contains(
            "Selected conversation",
            markup);
    }

    [Fact]
    public async Task Sidebar_ShouldRenderPinnedProjectsAndChatsInOrder()
    {
        var markup = await RenderAsync(
            [
                new StudioSidebarBibleEdition(
                    "lsg1910",
                    "Louis Segond 1910",
                    "fr")
            ],
            [
                new StudioSidebarConversation(
                    "/conversations/33333333-3333-3333-3333-333333333333",
                    "Free chat",
                    false)
            ],
            ApplicationLanguage.English,
            [
                new StudioSidebarPinnedItem(
                    "/conversations/11111111-1111-1111-1111-111111111111",
                    "Pinned chat",
                    false,
                    false)
            ],
            [
                new StudioSidebarProject(
                    "sidebar-project-22222222222222222222222222222222",
                    "Church history",
                    [
                        new StudioSidebarConversation(
                            "/conversations/22222222-2222-2222-2222-222222222222",
                            "Council of Nicaea",
                            true)
                    ])
            ]);

        var libraryIndex = markup.IndexOf(
            "Library",
            StringComparison.Ordinal);

        var pinnedIndex = markup.IndexOf(
            "Pinned",
            StringComparison.Ordinal);

        var projectsIndex = markup.IndexOf(
            "Projects",
            StringComparison.Ordinal);

        var chatsIndex = markup.IndexOf(
            "Chats",
            StringComparison.Ordinal);

        Assert.True(libraryIndex >= 0);
        Assert.True(pinnedIndex > libraryIndex);
        Assert.True(projectsIndex > pinnedIndex);
        Assert.True(chatsIndex > projectsIndex);
        Assert.Contains("Pinned chat", markup);
        Assert.Contains("Church history", markup);
        Assert.Contains("Council of Nicaea", markup);
        Assert.Contains("Free chat", markup);
    }

    [Fact]
    public async Task Sidebar_ShouldAlwaysRenderCoreSections()
    {
        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            Array.Empty<StudioSidebarConversation>(),
            ApplicationLanguage.English);

        var libraryIndex = markup.IndexOf(
            "Library",
            StringComparison.Ordinal);

        var pinnedIndex = markup.IndexOf(
            "Pinned",
            StringComparison.Ordinal);

        var projectsIndex = markup.IndexOf(
            "Projects",
            StringComparison.Ordinal);

        var chatsIndex = markup.IndexOf(
            "Chats",
            StringComparison.Ordinal);

        Assert.True(libraryIndex >= 0);
        Assert.True(pinnedIndex > libraryIndex);
        Assert.True(projectsIndex > pinnedIndex);
        Assert.True(chatsIndex > projectsIndex);
        Assert.Contains("No pinned items.", markup);
        Assert.Contains("No projects yet.", markup);
        Assert.Contains("No unassigned chats.", markup);
        Assert.DoesNotContain(">Trash<", markup);
    }

    [Fact]
    public async Task Sidebar_ShouldRenderProjectActionsInMenus()
    {
        var projectId =
            Guid.Parse("55555555-5555-5555-5555-555555555555");

        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            Array.Empty<StudioSidebarConversation>(),
            ApplicationLanguage.English,
            projects:
            [
                new StudioSidebarProject(
                    projectId,
                    "sidebar-project-55555555555555555555555555555555",
                    "Test project",
                    true,
                    Array.Empty<StudioSidebarConversation>())
            ]);

        Assert.Contains(
            "id=\"projects-action-trigger\"",
            markup);

        Assert.Contains(
            "id=\"projects-action-menu\"",
            markup);

        Assert.Contains(
            "popover=\"auto\"",
            markup);

        Assert.Contains(
            "New project",
            markup);

        Assert.Contains(
            "class=\"menu-bubble project-menu-trigger\"",
            markup);

        Assert.Contains(
            "Project actions: Test project",
            markup);

        Assert.Contains("Unpin", markup);
        Assert.Contains("Rename", markup);
        Assert.Contains("Delete", markup);
        Assert.DoesNotContain(
            "class=\"section-toolbar\"",
            markup);
        Assert.DoesNotContain(
            "class=\"project-actions\"",
            markup);
    }

    [Fact]
    public async Task Sidebar_ShouldRenderRecoverableDeletedConversations()
    {
        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            Array.Empty<StudioSidebarConversation>(),
            ApplicationLanguage.English,
            deletedConversations:
            [
                new StudioSidebarDeletedConversation(
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    "Recoverable chat",
                    DateTimeOffset.Parse("2026-08-03T12:00:00Z"))
            ]);

        Assert.Contains(">Trash<", markup);
        Assert.Contains("Recoverable chat", markup);
        Assert.Contains("Restore", markup);
        Assert.DoesNotContain("permanent", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sidebar_ShouldRenderAccountMenuWithAvailableActionsOnly()
    {
        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            Array.Empty<StudioSidebarConversation>(),
            ApplicationLanguage.English);

        Assert.Contains("class=\"account-trigger\"", markup);
        Assert.Contains("popovertarget=\"local-account-menu\"", markup);
        Assert.Contains("class=\"account-menu-popover\"", markup);
        Assert.Contains("popover=\"auto\"", markup);
        Assert.Contains("Mallory", markup);
        Assert.Contains("Admin", markup);
        Assert.Contains("mallory@example.com", markup);
        Assert.Contains("title=\"mallory@example.com\"", markup);
        Assert.Contains("class=\"account-email\"", markup);
        Assert.Contains("href=\"/settings\"", markup);
        Assert.Contains("Settings", markup);
        Assert.Contains("href=\"/administration/accounts\"", markup);
        Assert.Contains("href=\"/administration/access\"", markup);
        Assert.Contains("href=\"/administration/ai\"", markup);
        Assert.Contains("href=\"/administration/agents\"", markup);
        Assert.Contains("class=\"account-admin-submenu\"", markup);
        Assert.Contains("class=\"account-menu-item account-admin-submenu-trigger\"", markup);
        Assert.Contains("href=\"/administration/accounts\" aria-haspopup=\"menu\"", markup);
        Assert.Contains("class=\"account-admin-submenu-panel\"", markup);
        Assert.Contains(">Administration<", markup);
        Assert.Contains(">Accounts<", markup);
        Assert.Contains(">Groups and permissions<", markup);
        Assert.DoesNotContain("Upgrade plan", markup);
        Assert.Contains("Sign out", markup);
    }

    [Fact]
    public async Task Sidebar_ShouldExposePersonalSettingsButNotAdministrationToReader()
    {
        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            Array.Empty<StudioSidebarConversation>(),
            ApplicationLanguage.English,
            permissions: [SystemPermissions.AccessStudio],
            roles: [SystemRoles.Reader]);

        Assert.Contains("href=\"/settings\"", markup);
        Assert.Contains(">Settings<", markup);
        Assert.DoesNotContain(">Administration<", markup);
        Assert.DoesNotContain("href=\"/administration/", markup);
        Assert.Contains("Reader", markup);
    }

    [Fact]
    public async Task Sidebar_ShouldRenderDocumentManagerWorkspace()
    {
        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            Array.Empty<StudioSidebarConversation>(),
            ApplicationLanguage.French);

        Assert.Contains("href=\"/document-manager\"", markup);
        Assert.Contains("Gestion des documents", markup);
        Assert.Contains("href=\"/editorial-review\"", markup);
        Assert.Contains(
            "Revue éditoriale",
            System.Net.WebUtility.HtmlDecode(markup));
    }

    private static async Task<string> RenderAsync(
        IReadOnlyList<StudioSidebarBibleEdition> bibleEditions,
        IReadOnlyList<StudioSidebarConversation> conversations,
        ApplicationLanguage language,
        IReadOnlyList<StudioSidebarPinnedItem>? pinnedItems = null,
        IReadOnlyList<StudioSidebarProject>? projects = null,
        IReadOnlyList<StudioSidebarDeletedConversation>?
            deletedConversations = null,
        IReadOnlyList<string>? permissions = null,
        IReadOnlyList<string>? roles = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
        {
            foreach (var permission in SystemPermissions.All)
            {
                options.AddPolicy(
                    permission,
                    policy => policy.RequireClaim(
                        SystemPermissions.ClaimType,
                        permission));
            }

            options.AddPolicy(
                SystemPolicies.ManageAccess,
                policy => policy.RequireAssertion(context =>
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageGroups) ||
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageRoles)));
            options.AddPolicy(
                SystemPolicies.ViewIdentityAdministration,
                policy => policy.RequireAssertion(context =>
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageAccounts) ||
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageGroups) ||
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageRoles)));
            options.AddPolicy(
                SystemPolicies.ViewAdministration,
                policy => policy.RequireAssertion(context =>
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageAccounts) ||
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageGroups) ||
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageRoles) ||
                    context.User.HasClaim(
                        SystemPermissions.ClaimType,
                        SystemPermissions.ManageSettings)));
        });

        using var serviceProvider =
            services.BuildServiceProvider();

        var loggerFactory =
            serviceProvider.GetRequiredService<ILoggerFactory>();

        await using var renderer =
            new HtmlRenderer(
                serviceProvider,
                loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(
            async () =>
            {
                var parameterValues =
                    new Dictionary<string, object?>
                        {
                            [nameof(StudioSidebar.BibleEditions)] =
                                bibleEditions,
                            [nameof(StudioSidebar.Conversations)] =
                                conversations,
                            [nameof(StudioSidebar.PinnedItems)] =
                                pinnedItems ??
                                Array.Empty<StudioSidebarPinnedItem>(),
                            [nameof(StudioSidebar.Projects)] =
                                projects ??
                                Array.Empty<StudioSidebarProject>(),
                            [nameof(StudioSidebar.DeletedConversations)] =
                                deletedConversations ??
                                Array.Empty<StudioSidebarDeletedConversation>(),
                            [nameof(StudioSidebar.Language)] =
                                language
                        };

                var identity = new ClaimsIdentity(
                    (permissions ?? SystemPermissions.All).Select(permission =>
                        new Claim(SystemPermissions.ClaimType, permission)),
                    authenticationType: "Test",
                    nameType: ClaimTypes.Name,
                    roleType: ClaimTypes.Role);
                identity.AddClaim(new Claim(ClaimTypes.Name, "Mallory"));
                identity.AddClaim(new Claim(ClaimTypes.Email, "mallory@example.com"));
                foreach (var role in roles ?? [SystemRoles.Administrator])
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
                var authenticationState = Task.FromResult(
                    new AuthenticationState(new ClaimsPrincipal(identity)));
                RenderFragment childContent = builder =>
                {
                    builder.OpenComponent<StudioSidebar>(0);
                    builder.AddMultipleAttributes(
                        1,
                        parameterValues.Select(parameter =>
                            new KeyValuePair<string, object>(
                                parameter.Key,
                                parameter.Value!)));
                    builder.CloseComponent();
                };
                var wrapperParameters = ParameterView.FromDictionary(
                    new Dictionary<string, object?>
                    {
                        [nameof(CascadingValue<Task<AuthenticationState>>.Value)] = authenticationState,
                        [nameof(CascadingValue<Task<AuthenticationState>>.IsFixed)] = true,
                        [nameof(CascadingValue<Task<AuthenticationState>>.ChildContent)] = childContent
                    });
                var component = await renderer.RenderComponentAsync<
                    CascadingValue<Task<AuthenticationState>>>(wrapperParameters);

                return component.ToHtmlString();
            });
    }
}
