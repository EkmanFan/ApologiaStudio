using ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

namespace ApologiaStudio.Web.DocumentManager;

public sealed record DocumentManagerConsumerOptions(
    bool Enabled,
    DocumentManagerHttpOptions? Manager,
    string? NotificationSharedSecret,
    string? DeliveryReplayApiKey,
    TimeSpan ReconciliationInterval,
    TimeSpan RetryInterval,
    TimeSpan MaximumNotificationAge,
    TimeSpan RequestTimeout)
{
    public static DocumentManagerConsumerOptions FromConfiguration(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var enabled = configuration.GetValue<bool>(
            "DocumentManagerConsumer:Enabled");
        var reconciliationInterval = ReadPositiveSeconds(
            configuration,
            "DocumentManagerConsumer:ReconciliationSeconds",
            300);
        var retryInterval = ReadPositiveSeconds(
            configuration,
            "DocumentManagerConsumer:RetrySeconds",
            10);
        var maximumNotificationAge = ReadPositiveSeconds(
            configuration,
            "DocumentManagerConsumer:MaximumNotificationAgeSeconds",
            300);
        var requestTimeout = ReadPositiveSeconds(
            configuration,
            "DocumentManagerConsumer:RequestTimeoutSeconds",
            120);

        if (!enabled)
        {
            return new DocumentManagerConsumerOptions(
                false,
                null,
                null,
                null,
                reconciliationInterval,
                retryInterval,
                maximumNotificationAge,
                requestTimeout);
        }

        var managerUrl = Require(
            configuration["DocumentManagerConsumer:ManagerUrl"],
            "ManagerUrl");
        if (!Uri.TryCreate(managerUrl, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException(
                "DocumentManagerConsumer:ManagerUrl must be an absolute URL.");
        }

        var consumerKey = Require(
            configuration["DocumentManagerConsumer:ConsumerKey"],
            "ConsumerKey");
        var consumerId = Require(
            configuration["DocumentManagerConsumer:ConsumerId"],
            "ConsumerId");
        var notificationSecret = Require(
            configuration["DocumentManagerConsumer:NotificationSecret"],
            "NotificationSecret");

        if (notificationSecret.Length < 32 || ContainsNewLine(notificationSecret))
        {
            throw new InvalidOperationException(
                "DocumentManagerConsumer:NotificationSecret must contain at least 32 characters and no line breaks.");
        }

        var deliveryReplayApiKey = ReadOptional(
            configuration["DocumentManagerConsumer:DeliveryReplayApiKey"]);
        if (deliveryReplayApiKey is not null &&
            (deliveryReplayApiKey.Length < 32 ||
             ContainsNewLine(deliveryReplayApiKey)))
        {
            throw new InvalidOperationException(
                "DocumentManagerConsumer:DeliveryReplayApiKey must contain at least 32 characters and no line breaks when configured.");
        }

        return new DocumentManagerConsumerOptions(
            true,
            new DocumentManagerHttpOptions(
                baseAddress,
                consumerKey,
                consumerId,
                ReadPositiveLong(
                    configuration,
                    "DocumentManagerConsumer:MaximumResultBytes",
                    DocumentManagerHttpOptions.DefaultMaximumResultBytes),
                ReadPositiveLong(
                    configuration,
                    "DocumentManagerConsumer:MaximumVisualAssetBytes",
                    DocumentManagerHttpOptions.DefaultMaximumVisualAssetBytes)),
            notificationSecret,
            deliveryReplayApiKey,
            reconciliationInterval,
            retryInterval,
            maximumNotificationAge,
            requestTimeout);
    }

    private static TimeSpan ReadPositiveSeconds(
        IConfiguration configuration,
        string key,
        int defaultSeconds)
    {
        var seconds = configuration.GetValue<int?>(key) ?? defaultSeconds;
        return seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : throw new InvalidOperationException(
                $"Configuration '{key}' must be a positive number of seconds.");
    }

    private static long ReadPositiveLong(
        IConfiguration configuration,
        string key,
        long defaultValue)
    {
        var value = configuration.GetValue<long?>(key) ?? defaultValue;
        return value > 0
            ? value
            : throw new InvalidOperationException(
                $"Configuration '{key}' must be positive.");
    }

    private static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"DocumentManagerConsumer:{name} is required when the consumer is enabled.")
            : value.Trim();

    private static string? ReadOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static bool ContainsNewLine(string value) =>
        value.Contains('\r') || value.Contains('\n');

    public bool CanRequestReplay =>
        Enabled && Manager is not null && DeliveryReplayApiKey is not null;
}
