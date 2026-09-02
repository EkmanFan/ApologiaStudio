using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApologiaStudio.Web.DocumentManager;

public sealed record DocumentManagerResultAvailableNotification(
    Guid NotificationId,
    string ConsumerId,
    DateTimeOffset OccurredAtUtc);

public static class DocumentManagerNotificationAuthenticator
{
    public const string SignatureHeader =
        "X-Manager-Notification-Signature";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public static bool TryAuthenticate(
        ReadOnlySpan<byte> payload,
        string? signatureHeader,
        DocumentManagerConsumerOptions options,
        TimeProvider timeProvider,
        out DocumentManagerResultAvailableNotification? notification)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        notification = null;

        if (!options.Enabled ||
            options.Manager is null ||
            string.IsNullOrEmpty(options.NotificationSharedSecret) ||
            !TryReadSignature(signatureHeader, out var receivedSignature))
        {
            return false;
        }

        var expectedSignature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(options.NotificationSharedSecret),
            payload);

        if (!CryptographicOperations.FixedTimeEquals(
                receivedSignature,
                expectedSignature))
        {
            return false;
        }

        try
        {
            notification = JsonSerializer.Deserialize<
                DocumentManagerResultAvailableNotification>(
                payload,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (notification is null ||
            notification.NotificationId == Guid.Empty ||
            !string.Equals(
                notification.ConsumerId,
                options.Manager.ConsumerId,
                StringComparison.Ordinal))
        {
            notification = null;
            return false;
        }

        var age = timeProvider.GetUtcNow() - notification.OccurredAtUtc;
        if (age.Duration() > options.MaximumNotificationAge)
        {
            notification = null;
            return false;
        }

        return true;
    }

    private static bool TryReadSignature(
        string? header,
        out byte[] signature)
    {
        signature = [];

        const string prefix = "sha256=";
        if (string.IsNullOrWhiteSpace(header) ||
            !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hexadecimal = header[prefix.Length..];
        if (hexadecimal.Length != 64)
        {
            return false;
        }

        try
        {
            signature = Convert.FromHexString(hexadecimal);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
