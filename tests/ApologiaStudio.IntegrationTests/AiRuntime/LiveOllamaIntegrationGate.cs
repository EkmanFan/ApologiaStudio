namespace ApologiaStudio.IntegrationTests.AiRuntime;

/// <summary>
/// Opt-in switch for integration tests that drive a real Ollama instance.
/// </summary>
/// <remarks>
/// These tests load a model, which costs several gigabytes of VRAM for minutes
/// after the suite ends. Running them by default made an ordinary commit
/// mobilise the GPU and could silently distort a benchmark, a training run or an
/// evaluation campaign happening at the same time — EVAL-6 hit exactly that.
///
/// The switch is deliberately separate from <c>OLLAMA_EVALUATIONS_ENABLED</c>:
/// these are live integration tests of the runtime, not model evaluations, and
/// someone enabling one should not silently enable the other.
/// </remarks>
internal static class LiveOllamaIntegrationGate
{
    #region Variables and Constants

    /// <summary>
    /// Environment variable that enables live Ollama integration tests.
    /// </summary>
    public const string EnabledVariable =
        "OLLAMA_INTEGRATION_TESTS_ENABLED";

    #endregion

    #region Methods

    /// <summary>
    /// Gets whether live Ollama integration tests are explicitly enabled.
    /// </summary>
    public static bool IsEnabled() =>
        IsEnabled(
            Environment.GetEnvironmentVariable(
                EnabledVariable));

    /// <summary>
    /// Decides whether a given switch value enables live tests.
    /// </summary>
    /// <remarks>
    /// Only an explicit "true" enables them. Anything else — absent, empty,
    /// "1", "yes" — leaves them off, so the expensive path is never reached by
    /// a value someone assumed would work.
    /// </remarks>
    /// <param name="value">Raw environment-variable value.</param>
    /// <returns>Whether live tests may run.</returns>
    public static bool IsEnabled(
        string? value) =>
        string.Equals(
            value,
            "true",
            StringComparison.OrdinalIgnoreCase);

    #endregion
}
