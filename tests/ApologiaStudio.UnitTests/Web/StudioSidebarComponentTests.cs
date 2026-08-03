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
            "No chats yet.",
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

    private static async Task<string> RenderAsync(
        IReadOnlyList<StudioSidebarBibleEdition> bibleEditions,
        IReadOnlyList<StudioSidebarConversation> conversations,
        ApplicationLanguage language)
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
