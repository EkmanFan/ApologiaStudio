using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.Components.Navigation;
using Microsoft.AspNetCore.Components;
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
    public async Task Sidebar_ShouldHideEmptyPinnedAndTrashButShowProjects()
    {
        var markup = await RenderAsync(
            Array.Empty<StudioSidebarBibleEdition>(),
            Array.Empty<StudioSidebarConversation>(),
            ApplicationLanguage.English);

        Assert.DoesNotContain(">Pinned<", markup);
        Assert.Contains(">Projects<", markup);
        Assert.Contains("New project", markup);
        Assert.Contains(">Chats<", markup);
        Assert.DoesNotContain(">Trash<", markup);
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

    private static async Task<string> RenderAsync(
        IReadOnlyList<StudioSidebarBibleEdition> bibleEditions,
        IReadOnlyList<StudioSidebarConversation> conversations,
        ApplicationLanguage language,
        IReadOnlyList<StudioSidebarPinnedItem>? pinnedItems = null,
        IReadOnlyList<StudioSidebarProject>? projects = null,
        IReadOnlyList<StudioSidebarDeletedConversation>?
            deletedConversations = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

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
                var parameters =
                    ParameterView.FromDictionary(
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
                        });

                var component =
                    await renderer.RenderComponentAsync<
                        StudioSidebar>(parameters);

                return component.ToHtmlString();
            });
    }
}
