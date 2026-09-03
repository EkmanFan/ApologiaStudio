namespace ApologiaStudio.Web.DocumentManager;

public sealed record DocumentManagerUiOptions(Uri Address)
{
    private const string ConfigurationKey =
        "DocumentManager:UiUrl";

    public Uri SessionBridgeAddress { get; } =
        new("/document-manager/connect", UriKind.Relative);

    public static DocumentManagerUiOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[ConfigurationKey];

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var address))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must be an absolute URI.");
        }

        var usesHttps =
            address.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);

        var usesLoopbackHttp =
            address.Scheme.Equals(
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) &&
            address.IsLoopback;

        if (!usesHttps && !usesLoopbackHttp)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} must use HTTPS, except for a local loopback address.");
        }

        return new DocumentManagerUiOptions(address);
    }
}
