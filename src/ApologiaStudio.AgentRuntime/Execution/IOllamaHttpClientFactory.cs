namespace ApologiaStudio.AgentRuntime.Execution;

public interface IOllamaHttpClientFactory
{
    HttpClient Create(
        Uri baseAddress,
        TimeSpan timeout);
}
