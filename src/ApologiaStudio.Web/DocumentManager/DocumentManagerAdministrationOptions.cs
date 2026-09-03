using ApologiaStudio.Application.Knowledge.DocumentProcessing;
using ApologiaStudio.Domain.Users;

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
    DocumentManagerAdministrationOptions options,
    IHttpContextAccessor httpContextAccessor)
    : IDocumentManagerAdministrationAuthorizer
{
    public bool IsAuthorized =>
        options.Enabled &&
        httpContextAccessor.HttpContext?.User.HasClaim(
            SystemPermissions.ClaimType,
            SystemPermissions.PurgeEditorial) is true;
}
