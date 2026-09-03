using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApologiaStudio.Domain.Users;
using Microsoft.AspNetCore.WebUtilities;

namespace ApologiaStudio.Web.DocumentManager;

public sealed record DocumentManagerSessionTicketPayload(
    string Issuer,
    string Audience,
    string Subject,
    string DisplayName,
    string Email,
    string Language,
    IReadOnlyList<string> Permissions,
    long IssuedAtUnixSeconds,
    long ExpiresAtUnixSeconds,
    string Nonce);

public sealed class DocumentManagerSessionTicketIssuer(
    DocumentManagerSessionBridgeOptions options,
    TimeProvider timeProvider)
{
    private static readonly IReadOnlySet<string> ProjectedPermissions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            SystemPermissions.OperateDocumentManager,
            SystemPermissions.ReplayDocumentDelivery,
            SystemPermissions.PurgeManagerCustody
        };

    public string Issue(
        ClaimsPrincipal principal,
        ApplicationLanguage language)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out _))
        {
            throw new InvalidOperationException(
                "The authenticated Apologia account has no valid identifier.");
        }

        var permissions = principal.FindAll(SystemPermissions.ClaimType)
            .Select(claim => claim.Value)
            .Where(ProjectedPermissions.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!permissions.Contains(
                SystemPermissions.OperateDocumentManager,
                StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The account is not allowed to operate Document Manager.");
        }

        var issuedAt = timeProvider.GetUtcNow();
        var payload = new DocumentManagerSessionTicketPayload(
            options.Issuer,
            options.Audience,
            subject,
            principal.Identity?.Name ?? string.Empty,
            principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            language == ApplicationLanguage.English ? "en" : "fr",
            permissions,
            issuedAt.ToUnixTimeSeconds(),
            issuedAt.Add(options.TicketLifetime).ToUnixTimeSeconds(),
            WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24)));
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(
            payload,
            JsonSerializerOptions.Web);
        var payloadSegment = WebEncoders.Base64UrlEncode(payloadBytes);
        var signedValue = $"v1.{payloadSegment}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.SharedSecret));
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(signedValue));
        return $"{signedValue}.{WebEncoders.Base64UrlEncode(signature)}";
    }
}
