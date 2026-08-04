using ApologiaStudio.AgentRuntime.Routing.Semantic;

namespace ApologiaStudio.UnitTests.AgentRuntime.Routing;

public sealed class OllamaRoutingSettingsTests
{
    [Fact]
    public void ToOptions_ShouldRejectNonLoopbackAddress()
    {
        var settings =
            new OllamaRoutingSettings(
                "https://example.com",
                "qwen3:8b",
                60,
                "10m");

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    OllamaRoutingSettingsValidator.ToOptions(
                        settings));

        Assert.Contains(
            "loopback",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToOptions_ShouldNormalizeValidSettings()
    {
        var settings =
            new OllamaRoutingSettings(
                "http://localhost:11434",
                " qwen3:8b ",
                45,
                " 10m ");

        var options =
            OllamaRoutingSettingsValidator.ToOptions(settings);

        Assert.Equal(
            new Uri("http://localhost:11434/"),
            options.BaseAddress);
        Assert.Equal("qwen3:8b", options.Model);
        Assert.Equal(
            TimeSpan.FromSeconds(45),
            options.RequestTimeout);
        Assert.Equal("10m", options.KeepAlive);
    }

    [Fact]
    public async Task FileStore_ShouldPersistAndReloadSettings()
    {
        var directory =
            Path.Combine(
                Path.GetTempPath(),
                $"apologia-routing-{Guid.NewGuid():N}");

        var filePath =
            Path.Combine(
                directory,
                "ollama-routing-settings.json");

        var defaults =
            new OllamaRoutingSettings(
                "http://127.0.0.1:11434/",
                "qwen3:8b",
                60,
                "10m");

        try
        {
            var store =
                new FileOllamaRoutingSettingsStore(
                    filePath,
                    defaults);

            var updated =
                new OllamaRoutingSettings(
                    "http://localhost:11434/",
                    "qwen3:14b",
                    90,
                    "30m");

            await store.SaveAsync(updated);

            var reloaded =
                new FileOllamaRoutingSettingsStore(
                    filePath,
                    defaults);

            Assert.Equal(updated, reloaded.Current);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }
}
