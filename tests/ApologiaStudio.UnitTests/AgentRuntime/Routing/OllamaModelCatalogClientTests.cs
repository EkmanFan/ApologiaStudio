using System.Net;
using System.Text;
using ApologiaStudio.AgentRuntime.Routing.Semantic;

namespace ApologiaStudio.UnitTests.AgentRuntime.Routing;

public sealed class OllamaModelCatalogClientTests
{
    [Fact]
    public async Task ListLocalModelsAsync_ShouldReturnSortedLocalModels()
    {
        const string json =
            """
            {
              "models": [
                {
                  "name": "qwen3:8b",
                  "details": {
                    "family": "qwen3",
                    "parameter_size": "8.2B",
                    "quantization_level": "Q4_K_M"
                  }
                },
                {
                  "name": "mixtral:instruct",
                  "details": {
                    "family": "llama",
                    "parameter_size": "46.7B",
                    "quantization_level": "Q4_0"
                  }
                }
              ]
            }
            """;

        var handler =
            new StubHttpMessageHandler(
                request =>
                {
                    Assert.Equal(
                        HttpMethod.Get,
                        request.Method);
                    Assert.Equal(
                        new Uri(
                            "http://127.0.0.1:11434/api/tags"),
                        request.RequestUri);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                json,
                                Encoding.UTF8,
                                "application/json")
                    };
                });

        using var httpClient = new HttpClient(handler);

        var client =
            new OllamaModelCatalogClient(httpClient);

        var models =
            await client.ListLocalModelsAsync(
                new Uri("http://127.0.0.1:11434/"));

        Assert.Equal(2, models.Count);
        Assert.Equal("mixtral:instruct", models[0].Name);
        Assert.Equal("qwen3:8b", models[1].Name);
        Assert.Contains("8.2B", models[1].DisplayName);
        Assert.Contains("Q4_K_M", models[1].DisplayName);
    }

    [Fact]
    public async Task ListLocalModelsAsync_ShouldRejectRemoteAddress()
    {
        using var httpClient =
            new HttpClient(
                new StubHttpMessageHandler(
                    _ =>
                        throw new InvalidOperationException(
                            "HTTP must not be called.")));

        var client =
            new OllamaModelCatalogClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                client.ListLocalModelsAsync(
                    new Uri("https://example.com")));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
