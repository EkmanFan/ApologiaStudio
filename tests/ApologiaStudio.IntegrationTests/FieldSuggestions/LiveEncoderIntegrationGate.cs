namespace ApologiaStudio.IntegrationTests.FieldSuggestions;

/// <summary>
/// Opt-in switch for the smoke test that drives the real encoder worker.
/// </summary>
/// <remarks>
/// The worker holds two resident models and roughly 3.4 GiB of RSS. An ordinary
/// commit must not require it to be running, and must not silently start it.
/// The switch is separate from the Ollama ones: these are different capabilities
/// on different hardware, and enabling one must never enable another.
/// </remarks>
internal static class LiveEncoderIntegrationGate
{
    #region Variables and Constants

    public const string EnabledVariable = "ENCODER_SMOKE_TESTS_ENABLED";

    public const string EndpointVariable = "ENCODER_WORKER_BASE_ADDRESS";

    private const string DefaultEndpoint = "http://127.0.0.1:5099/";

    #endregion

    #region Methods

    public static bool IsEnabled() =>
        IsEnabled(Environment.GetEnvironmentVariable(EnabledVariable));

    /// <summary>
    /// Only an explicit "true" enables the smoke test.
    /// </summary>
    public static bool IsEnabled(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    public static Uri Endpoint()
    {
        var configured = Environment.GetEnvironmentVariable(EndpointVariable);

        return new Uri(
            string.IsNullOrWhiteSpace(configured) ? DefaultEndpoint : configured);
    }

    #endregion
}
