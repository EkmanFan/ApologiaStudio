using ApologiaStudio.AgentRuntime.Routing.Semantic;
using Xunit.Abstractions;

namespace ApologiaStudio.Evaluations.Routing;

public sealed class OllamaSemanticRoutingDiagnosticTests(
    ITestOutputHelper output)
{
    [Trait("Category", "LocalModel")]
    [Fact]
    public async Task Classifier_ShouldRouteClovisToHistorian_WhenEnabled()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(
                    "OLLAMA_EVALUATIONS_ENABLED"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine(
                "Local model evaluation was not enabled.");

            return;
        }

        var baseUrl =
            Environment.GetEnvironmentVariable(
                "OLLAMA_BASE_URL")
            ?? "http://127.0.0.1:11434";

        var model =
            Environment.GetEnvironmentVariable(
                "OLLAMA_ROUTING_MODEL")
            ?? "qwen3:8b";

        var options =
            new OllamaRoutingOptions
            {
                BaseAddress =
                    new Uri(
                        baseUrl.TrimEnd('/') + "/"),
                Model = model,
                RequestTimeout =
                    TimeSpan.FromSeconds(60),
                KeepAlive = "10m"
            };

        using var classifier =
            new OllamaSemanticRoutingClassifier(
                new HttpClient
                {
                    BaseAddress =
                        options.BaseAddress,
                    Timeout =
                        options.RequestTimeout
                },
                options);

        var result =
            await classifier.ClassifyAsync(
                "Quel âge avait Clovis lors de son sacre ?",
                CancellationToken.None);

        output.WriteLine(
            $"Agent: {result.AgentSlug}");

        output.WriteLine(
            $"Confidence: {result.Confidence:F2}");

        output.WriteLine(
            $"Reason: {result.Reason}");

        Assert.Equal(
            "historian",
            result.AgentSlug);

        Assert.InRange(
            result.Confidence,
            0.65,
            1.0);
    }
}
