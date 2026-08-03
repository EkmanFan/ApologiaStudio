using System.Net;
using ApologiaStudio.Application.BibleCorpora.Queries;
using ApologiaStudio.Application.BibleCorpora.Reader;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.Components.BibleReader;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ApologiaStudio.UnitTests.Web;

public sealed class BibleReaderPanelComponentTests
{
    [Fact]
    public async Task Reader_ShouldRenderChapterNavigationAndExactVerseLabels()
    {
        var edition = new BibleEditionSummary(
            "lsg1910",
            "Louis Segond 1910",
            "fr",
            "protestant-66");

        var book = new BibleBookSummary(
            "GEN",
            "Gen",
            1,
            "Genèse",
            "Gen",
            1);

        var view = new BibleReaderView(
            BibleReaderStatus.Ready,
            [edition],
            edition,
            [book],
            new BibleChapter(
                edition,
                book,
                1,
                [
                    new BibleVerseText(
                        "GEN",
                        1,
                        "1a",
                        1,
                        "Au commencement",
                        []),
                    new BibleVerseText(
                        "GEN",
                        1,
                        "1b",
                        2,
                        "Dieu créa",
                        [])
                ]),
            null,
            new BibleReaderLocation(
                "lsg1910",
                "EXO",
                1));

        var markup = await RenderAsync(
            view,
            ApplicationLanguage.French);

        var decodedMarkup = WebUtility.HtmlDecode(markup);

        Assert.Contains("Louis Segond 1910", decodedMarkup);
        Assert.Contains("Genèse 1", decodedMarkup);
        Assert.Contains(">1a<", markup);
        Assert.Contains("Au commencement", decodedMarkup);
        Assert.Contains(
            "href=\"/library/lsg1910/EXO/1\"",
            markup);

        Assert.Contains(
            "Sélectionnez un verset pour préparer une référence.",
            decodedMarkup);
    }

    [Fact]
    public async Task Reader_ShouldRenderAnExplicitUnknownEditionState()
    {
        var view = new BibleReaderView(
            BibleReaderStatus.EditionNotFound,
            [
                new BibleEditionSummary(
                    "lsg1910",
                    "Louis Segond 1910",
                    "fr",
                    "protestant-66")
            ]);

        var markup = await RenderAsync(
            view,
            ApplicationLanguage.English);

        Assert.Contains("Unknown edition", markup);
        Assert.Contains(
            "This edition does not exist or is unavailable.",
            markup);

        Assert.Contains(
            "href=\"/library/lsg1910\"",
            markup);
    }

    private static async Task<string> RenderAsync(
        BibleReaderView reader,
        ApplicationLanguage language)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NavigationManager>(
            new TestNavigationManager());
        services.AddSingleton<IJSRuntime>(
            new TestJsRuntime());

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
                            [nameof(BibleReaderPanel.Reader)] = reader,
                            [nameof(BibleReaderPanel.Language)] = language
                        });

                var component =
                    await renderer.RenderComponentAsync<
                        BibleReaderPanel>(parameters);

                return component.ToHtmlString();
            });
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize(
                "http://localhost/",
                "http://localhost/library/lsg1910/GEN/1");
        }

        protected override void NavigateToCore(
            string uri,
            bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    private sealed class TestJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            object?[]? args)
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
