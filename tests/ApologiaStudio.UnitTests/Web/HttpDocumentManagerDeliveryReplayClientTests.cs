using System.Net;
using System.Text.Json;
using ApologiaStudio.Web.DocumentManager;

namespace ApologiaStudio.UnitTests.Web;

public sealed class HttpDocumentManagerDeliveryReplayClientTests
{
    [Fact]
    public async Task Replay_UsesNarrowCredentialAndConfiguredConsumer()
    {
        var submissionId = Guid.NewGuid();
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var options = DocumentManagerConsumerOptions.FromConfiguration(
            DocumentManagerConsumerOptionsTests.CreateEnabledConfiguration());
        var client = new HttpDocumentManagerDeliveryReplayClient(
            httpClient,
            options);

        await client.ReplaySubmissionAsync(
            submissionId,
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            $"http://127.0.0.1:5080/api/manager-delivery-administration/submissions/{submissionId:D}/replay",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal(
            options.DeliveryReplayApiKey,
            handler.DeliveryReplayKey);

        using var document = JsonDocument.Parse(handler.Content);
        Assert.Equal(
            "apologia-studio",
            document.RootElement.GetProperty("consumerId").GetString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? DeliveryReplayKey { get; private set; }
        public string Content { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            DeliveryReplayKey = request.Headers
                .GetValues("X-Manager-Delivery-Replay-Key")
                .Single();
            Content = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
