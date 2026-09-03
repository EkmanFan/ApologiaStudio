using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.DocumentManager;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace ApologiaStudio.UnitTests.Web;

public sealed class DocumentManagerSessionTicketIssuerTests
{
    private const string SharedSecret =
        "manager-session-unit-test-shared-secret-2026";
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-09-03T10:00:00Z");

    [Fact]
    public void Issue_ProjectsOnlyManagerPermissionsIntoSignedShortLivedTicket()
    {
        var options = CreateOptions();
        var issuer = new DocumentManagerSessionTicketIssuer(
            options,
            new StubTimeProvider(Now));
        var principal = CreatePrincipal(
            SystemPermissions.OperateDocumentManager,
            SystemPermissions.ReplayDocumentDelivery,
            SystemPermissions.ManageAccounts);

        var ticket = issuer.Issue(principal, ApplicationLanguage.English);
        var segments = ticket.Split('.');

        Assert.Equal("v1", segments[0]);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SharedSecret));
        Assert.True(CryptographicOperations.FixedTimeEquals(
            WebEncoders.Base64UrlDecode(segments[2]),
            hmac.ComputeHash(Encoding.ASCII.GetBytes($"v1.{segments[1]}"))));
        var payload = JsonSerializer.Deserialize<DocumentManagerSessionTicketPayload>(
            WebEncoders.Base64UrlDecode(segments[1]),
            JsonSerializerOptions.Web);
        Assert.NotNull(payload);
        Assert.Equal("en", payload.Language);
        Assert.Equal(30, payload.ExpiresAtUnixSeconds - payload.IssuedAtUnixSeconds);
        Assert.Equal(
            [
                SystemPermissions.ReplayDocumentDelivery,
                SystemPermissions.OperateDocumentManager
            ],
            payload.Permissions);
        Assert.DoesNotContain(SystemPermissions.ManageAccounts, payload.Permissions);
    }

    [Fact]
    public void Issue_RejectsAReaderWithoutManagerOperationPermission()
    {
        var issuer = new DocumentManagerSessionTicketIssuer(
            CreateOptions(),
            new StubTimeProvider(Now));

        Assert.Throws<UnauthorizedAccessException>(() =>
            issuer.Issue(
                CreatePrincipal(SystemPermissions.AccessStudio),
                ApplicationLanguage.French));
    }

    [Fact]
    public void Options_RequireAStrongSecretAndSafeManagerAddress()
    {
        var manager = new DocumentManagerUiOptions(
            new Uri("https://manager.example/"));
        Assert.Throws<InvalidOperationException>(() =>
            DocumentManagerSessionBridgeOptions.FromConfiguration(
                CreateConfiguration("short"),
                manager));

        var options = DocumentManagerSessionBridgeOptions.FromConfiguration(
            CreateConfiguration(SharedSecret),
            manager);
        Assert.Equal(
            new Uri("https://manager.example/auth/apologia/exchange"),
            options.ExchangeAddress);
    }

    private static DocumentManagerSessionBridgeOptions CreateOptions() =>
        new(
            new Uri("https://manager.example/auth/apologia/exchange"),
            SharedSecret,
            "apologia-studio",
            "document-manager-ui",
            TimeSpan.FromSeconds(30));

    private static ClaimsPrincipal CreatePrincipal(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                "11111111-1111-1111-1111-111111111111"),
            new(ClaimTypes.Name, "Mallory"),
            new(ClaimTypes.Email, "mallory@example.test")
        };
        claims.AddRange(permissions.Select(permission =>
            new Claim(SystemPermissions.ClaimType, permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static IConfiguration CreateConfiguration(string secret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentManager:SessionBridge:SharedSecret"] = secret
            })
            .Build();

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
