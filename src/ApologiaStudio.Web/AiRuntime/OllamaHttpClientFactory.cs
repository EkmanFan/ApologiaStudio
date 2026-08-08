using ApologiaStudio.AgentRuntime.Execution;

namespace ApologiaStudio.Web.AiRuntime;

public sealed class OllamaHttpClientFactory(
    IHttpClientFactory httpClientFactory)
    : IOllamaHttpClientFactory
{
    public HttpClient Create(
        Uri baseAddress,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        var client =
            httpClientFactory.CreateClient(
                "ApologiaStudio.Ollama.Dynamic");

        client.BaseAddress = baseAddress;
        client.Timeout = timeout;

        return client;
    }
}
