using ApologiaStudio.Web.DocumentManager;
using Microsoft.Extensions.Configuration;

namespace ApologiaStudio.UnitTests.Web;

public sealed class DocumentManagerConsumerOptionsTests
{
    [Fact]
    public void Consumer_is_disabled_without_transport_credentials()
    {
        var options = DocumentManagerConsumerOptions.FromConfiguration(
            new ConfigurationBuilder().Build());

        Assert.False(options.Enabled);
        Assert.Null(options.Manager);
        Assert.Equal(TimeSpan.FromMinutes(5), options.ReconciliationInterval);
    }

    [Fact]
    public void Enabled_consumer_requires_complete_credentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DocumentManagerConsumer:Enabled"] = "true"
                })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => DocumentManagerConsumerOptions.FromConfiguration(
                configuration));
    }

    [Fact]
    public void Enabled_consumer_loads_manager_and_notification_settings()
    {
        var options = DocumentManagerConsumerOptions.FromConfiguration(
            CreateEnabledConfiguration());

        Assert.True(options.Enabled);
        Assert.Equal("apologia-studio", options.Manager!.ConsumerId);
        Assert.True(options.CanRequestReplay);
        Assert.Equal(TimeSpan.FromSeconds(450), options.ReconciliationInterval);
        Assert.Equal(TimeSpan.FromSeconds(12), options.RetryInterval);
    }

    internal static IConfiguration CreateEnabledConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DocumentManagerConsumer:Enabled"] = "true",
                    ["DocumentManagerConsumer:ManagerUrl"] =
                        "http://127.0.0.1:5080/",
                    ["DocumentManagerConsumer:ConsumerId"] =
                        "apologia-studio",
                    ["DocumentManagerConsumer:ConsumerKey"] =
                        "consumer-key-with-at-least-32-characters",
                    ["DocumentManagerConsumer:NotificationSecret"] =
                        "notification-secret-with-at-least-32-characters",
                    ["DocumentManagerConsumer:DeliveryReplayApiKey"] =
                        "delivery-replay-key-with-at-least-32-characters",
                    ["DocumentManagerConsumer:ReconciliationSeconds"] = "450",
                    ["DocumentManagerConsumer:RetrySeconds"] = "12"
                })
            .Build();
}
