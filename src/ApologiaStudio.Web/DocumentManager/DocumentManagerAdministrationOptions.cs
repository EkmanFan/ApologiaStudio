using ApologiaStudio.Application.Knowledge.DocumentProcessing;

namespace ApologiaStudio.Web.DocumentManager;

public sealed record DocumentManagerAdministrationOptions(bool Enabled)
{
    private const string ConfigurationKey =
        "DocumentManagerAdministration:Enabled";

    public static DocumentManagerAdministrationOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new DocumentManagerAdministrationOptions(
            configuration.GetValue<bool>(ConfigurationKey));
    }
}

public sealed class ConfiguredDocumentManagerAdministrationAuthorizer(
    DocumentManagerAdministrationOptions options)
    : IDocumentManagerAdministrationAuthorizer
{
    public bool IsAuthorized => options.Enabled;
}
