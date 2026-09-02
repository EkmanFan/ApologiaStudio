using System.Net.Http.Json;

namespace ApologiaStudio.Web.DocumentManager;

public sealed class HttpDocumentManagerDeliveryReplayClient(
    HttpClient httpClient,
    DocumentManagerConsumerOptions options)
    : IDocumentManagerDeliveryReplayClient
{
    private const string DeliveryReplayKeyHeader =
        "X-Manager-Delivery-Replay-Key";

    public async Task ReplaySubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Submission identifier cannot be empty.",
                nameof(submissionId));
        }

        if (!options.CanRequestReplay)
        {
            throw new InvalidOperationException(
                "Document Manager delivery replay is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(
                options.Manager!.BaseAddress,
                $"api/manager-delivery-administration/submissions/{submissionId:D}/replay"));
        request.Headers.Add(
            DeliveryReplayKeyHeader,
            options.DeliveryReplayApiKey);
        request.Content = JsonContent.Create(
            new ReplaySubmissionRequest(options.Manager.ConsumerId));

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Document Manager refused delivery replay with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                null,
                response.StatusCode);
        }
    }

    private sealed record ReplaySubmissionRequest(string ConsumerId);
}
