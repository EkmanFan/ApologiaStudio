namespace ApologiaStudio.Web.DocumentManager;

public static class DocumentManagerNotificationEndpoints
{
    private const int MaximumPayloadBytes = 4096;

    public static IEndpointRouteBuilder MapDocumentManagerNotificationEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/internal/document-manager/result-available",
            HandleAsync);

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        DocumentManagerConsumerOptions options,
        DocumentManagerConsumptionSignal signal,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Results.NotFound();
        }

        if (request.ContentLength is > MaximumPayloadBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var payload = await ReadBoundedAsync(
            request.Body,
            MaximumPayloadBytes,
            cancellationToken);
        if (payload is null)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var signature = request.Headers[
            DocumentManagerNotificationAuthenticator.SignatureHeader]
            .ToString();

        if (!DocumentManagerNotificationAuthenticator.TryAuthenticate(
                payload,
                signature,
                options,
                timeProvider,
                out _))
        {
            return Results.Unauthorized();
        }

        signal.Notify();
        return Results.Accepted();
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        Stream source,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream();
        var buffer = new byte[1024];

        while (true)
        {
            var count = await source.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken);
            if (count == 0)
            {
                return destination.ToArray();
            }

            if (destination.Length + count > maximumBytes)
            {
                return null;
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, count),
                cancellationToken);
        }
    }
}
