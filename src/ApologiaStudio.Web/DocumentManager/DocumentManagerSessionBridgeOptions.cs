namespace ApologiaStudio.Web.DocumentManager;

public sealed record DocumentManagerSessionBridgeOptions(
    Uri ExchangeAddress,
    string SharedSecret,
    string Issuer,
    string Audience,
    TimeSpan TicketLifetime)
{
    public static DocumentManagerSessionBridgeOptions FromConfiguration(
        IConfiguration configuration,
        DocumentManagerUiOptions uiOptions)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(uiOptions);

        var sharedSecret = configuration[
            "DocumentManager:SessionBridge:SharedSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(sharedSecret) || sharedSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "DocumentManager:SessionBridge:SharedSecret must contain at least 32 characters.");
        }

        var lifetimeSeconds = configuration.GetValue<int?>(
            "DocumentManager:SessionBridge:TicketLifetimeSeconds") ?? 30;
        if (lifetimeSeconds is < 10 or > 60)
        {
            throw new InvalidOperationException(
                "DocumentManager:SessionBridge:TicketLifetimeSeconds must be between 10 and 60.");
        }

        return new DocumentManagerSessionBridgeOptions(
            new Uri(uiOptions.Address, "auth/apologia/exchange"),
            sharedSecret,
            ReadOrDefault(
                configuration["DocumentManager:SessionBridge:Issuer"],
                "apologia-studio"),
            ReadOrDefault(
                configuration["DocumentManager:SessionBridge:Audience"],
                "document-manager-ui"),
            TimeSpan.FromSeconds(lifetimeSeconds));
    }

    private static string ReadOrDefault(string? value, string defaultValue)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? defaultValue : trimmed;
    }
}
