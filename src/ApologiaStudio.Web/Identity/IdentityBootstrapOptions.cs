namespace ApologiaStudio.Web.Identity;

public sealed record IdentityBootstrapOptions(
    bool Enabled,
    string? Email,
    string? Password,
    string DisplayName)
{
    public static IdentityBootstrapOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var enabled = configuration.GetValue<bool>(
            "IdentityBootstrap:Enabled");
        var email = ReadOptional(configuration["IdentityBootstrap:Email"]);
        var password = ReadOptional(configuration["IdentityBootstrap:Password"]);
        var displayName = ReadOptional(
            configuration["IdentityBootstrap:DisplayName"])
            ?? "Apologia Administrator";

        if (enabled &&
            (email is null || password is null || password.Length < 12))
        {
            throw new InvalidOperationException(
                "Identity bootstrap requires an e-mail address and a password of at least 12 characters.");
        }

        return new IdentityBootstrapOptions(
            enabled,
            email,
            password,
            displayName);
    }

    private static string? ReadOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
