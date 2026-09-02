using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApologiaStudio.Web.DocumentManager;

namespace ApologiaStudio.UnitTests.Web;

public sealed class DocumentManagerNotificationAuthenticatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 2, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Authentic_notification_is_accepted()
    {
        var options = CreateOptions();
        var payload = CreatePayload(Now, "apologia-studio");

        var authenticated =
            DocumentManagerNotificationAuthenticator.TryAuthenticate(
                payload,
                CreateSignature(payload, options.NotificationSharedSecret!),
                options,
                new FixedTimeProvider(Now),
                out var notification);

        Assert.True(authenticated);
        Assert.NotNull(notification);
    }

    [Fact]
    public void Altered_payload_is_rejected()
    {
        var options = CreateOptions();
        var original = CreatePayload(Now, "apologia-studio");
        var altered = CreatePayload(Now.AddSeconds(1), "apologia-studio");

        var authenticated =
            DocumentManagerNotificationAuthenticator.TryAuthenticate(
                altered,
                CreateSignature(original, options.NotificationSharedSecret!),
                options,
                new FixedTimeProvider(Now),
                out _);

        Assert.False(authenticated);
    }

    [Theory]
    [InlineData("another-consumer", 0)]
    [InlineData("apologia-studio", -301)]
    [InlineData("apologia-studio", 301)]
    public void Wrong_consumer_or_stale_notification_is_rejected(
        string consumerId,
        int offsetSeconds)
    {
        var options = CreateOptions();
        var payload = CreatePayload(
            Now.AddSeconds(offsetSeconds),
            consumerId);

        var authenticated =
            DocumentManagerNotificationAuthenticator.TryAuthenticate(
                payload,
                CreateSignature(payload, options.NotificationSharedSecret!),
                options,
                new FixedTimeProvider(Now),
                out _);

        Assert.False(authenticated);
    }

    private static DocumentManagerConsumerOptions CreateOptions() =>
        DocumentManagerConsumerOptions.FromConfiguration(
            DocumentManagerConsumerOptionsTests.CreateEnabledConfiguration());

    private static byte[] CreatePayload(
        DateTimeOffset occurredAtUtc,
        string consumerId) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new DocumentManagerResultAvailableNotification(
                Guid.NewGuid(),
                consumerId,
                occurredAtUtc),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string CreateSignature(
        byte[] payload,
        string secret)
    {
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            payload);
        return $"sha256={Convert.ToHexString(signature).ToLowerInvariant()}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
