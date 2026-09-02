using System.Net;
using System.Text;
using ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

namespace ApologiaStudio.UnitTests.Infrastructure.Knowledge;

public sealed class HttpDocumentManagerResultSourceTests
{
    [Fact]
    public async Task Source_uses_consumer_credentials_and_maps_a_claim()
    {
        const string json =
            """
            {
              "resultReference": "manager-result:one",
              "submissionId": "00000000-0000-0000-0000-000000000001",
              "processingUnitId": "00000000-0000-0000-0000-000000000002",
              "scope": {
                "kind": "pageRange",
                "startPhysicalPageNumber": 1,
                "endPhysicalPageNumber": 50,
                "title": "Part 1"
              },
              "schemaVersion": "document-processing-result-v4",
              "mediaType": "application/vnd.document-processing-result+json",
              "byteLength": 42,
              "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "availableAtUtc": "2026-09-02T14:00:00Z",
              "claimToken": "00000000-0000-0000-0000-000000000003",
              "claimExpiresAtUtc": "2026-09-02T14:05:00Z",
              "submissionManifest": {
                "submissionId": "00000000-0000-0000-0000-000000000001",
                "revision": 2,
                "sourceSha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "originalFileName": "book.pdf",
                "finalizedAtUtc": "2026-09-02T13:55:00Z",
                "expectedUnits": [
                  {
                    "processingUnitId": "00000000-0000-0000-0000-000000000002",
                    "ordinal": 1,
                    "scope": {
                      "kind": "pageRange",
                      "startPhysicalPageNumber": 1,
                      "endPhysicalPageNumber": 50,
                      "title": "Part 1"
                    }
                  }
                ]
              }
            }
            """;
        var handler =
            new StubHttpMessageHandler(
                request =>
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal(
                        "/api/manager-consumers/results/claims",
                        request.RequestUri!.AbsolutePath);
                    Assert.Equal(
                        "consumer-key-with-at-least-32-characters",
                        Assert.Single(
                            request.Headers.GetValues(
                                "X-Manager-Consumer-Key")));
                    Assert.Equal(
                        "apologia-studio-test",
                        Assert.Single(
                            request.Headers.GetValues("X-Consumer-Id")));

                    return JsonResponse(HttpStatusCode.OK, json);
                });
        using var httpClient = new HttpClient(handler);
        var source = CreateSource(httpClient);

        var claim =
            await source.ClaimNextAsync(CancellationToken.None);

        Assert.NotNull(claim);
        Assert.Equal("manager-result:one", claim.ResultReference);
        Assert.Equal("pageRange", claim.Scope.Kind);
        Assert.Equal(1, claim.Scope.StartPhysicalPageNumber);
        Assert.Equal(50, claim.Scope.EndPhysicalPageNumber);
        Assert.Equal("Part 1", claim.Scope.Title);
        Assert.Equal(2, claim.SubmissionManifest.Revision);
        Assert.Equal("book.pdf", claim.SubmissionManifest.OriginalFileName);
        Assert.Single(claim.SubmissionManifest.ExpectedUnits);
    }

    [Fact]
    public async Task Source_returns_null_for_no_content_claim_response()
    {
        using var httpClient =
            new HttpClient(
                new StubHttpMessageHandler(
                    _ => new HttpResponseMessage(HttpStatusCode.NoContent)));
        var source = CreateSource(httpClient);

        var claim =
            await source.ClaimNextAsync(CancellationToken.None);

        Assert.Null(claim);
    }

    [Fact]
    public void Options_reject_clear_text_remote_transport()
    {
        Assert.Throws<ArgumentException>(() =>
            new DocumentManagerHttpOptions(
                new Uri("http://example.com"),
                "consumer-key-with-at-least-32-characters",
                "apologia-studio"));
    }

    private static HttpDocumentManagerResultSource CreateSource(
        HttpClient httpClient) =>
        new(
            httpClient,
            new DocumentManagerHttpOptions(
                new Uri("http://127.0.0.1:5080/"),
                "consumer-key-with-at-least-32-characters",
                "apologia-studio-test"));

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json) =>
        new(statusCode)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
