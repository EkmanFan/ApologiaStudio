namespace ApologiaStudio.IntegrationTests.AiRuntime;

/// <summary>
/// Guards the opt-in switch itself.
/// </summary>
/// <remarks>
/// The failure this prevents is silent: a gate that quietly starts defaulting to
/// on would put a model back in VRAM on every commit, and nothing in the suite
/// output would say so. Asserting the decision directly is the only way that
/// drift shows up as a failing test.
/// </remarks>
public sealed class LiveOllamaIntegrationGateTests
{
    [Fact]
    public void Live_tests_are_off_unless_explicitly_enabled()
    {
        Assert.False(
            LiveOllamaIntegrationGate.IsEnabled(
                null));
        Assert.False(
            LiveOllamaIntegrationGate.IsEnabled(
                string.Empty));
        Assert.False(
            LiveOllamaIntegrationGate.IsEnabled(
                "false"));
    }

    [Fact]
    public void Only_an_explicit_true_enables_live_tests()
    {
        Assert.True(
            LiveOllamaIntegrationGate.IsEnabled(
                "true"));
        Assert.True(
            LiveOllamaIntegrationGate.IsEnabled(
                "TRUE"));

        // Values someone might assume work must not enable the expensive path.
        Assert.False(
            LiveOllamaIntegrationGate.IsEnabled(
                "1"));
        Assert.False(
            LiveOllamaIntegrationGate.IsEnabled(
                "yes"));
    }

    [Fact]
    public void The_switch_is_distinct_from_the_evaluation_switch()
    {
        // Enabling model evaluations must not silently enable live runtime
        // integration tests, and the reverse.
        Assert.NotEqual(
            "OLLAMA_EVALUATIONS_ENABLED",
            LiveOllamaIntegrationGate.EnabledVariable);
        Assert.Equal(
            "OLLAMA_INTEGRATION_TESTS_ENABLED",
            LiveOllamaIntegrationGate.EnabledVariable);
    }
}
