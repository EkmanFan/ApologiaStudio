using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;

namespace ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

public sealed class HttpDocumentManagerResultSource(
    HttpClient httpClient,
    DocumentManagerHttpOptions options)
    : IDocumentManagerResultSource
{
    private const string ConsumerKeyHeader =
        "X-Manager-Consumer-Key";

    private const string ConsumerIdHeader =
        "X-Consumer-Id";

    private const string ClaimTokenHeader =
        "X-Result-Claim-Token";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<DocumentManagerResultClaim?> ClaimNextAsync(
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            "api/manager-consumers/results/claims");
        using var response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        var dto =
            await response.Content.ReadFromJsonAsync<ResultAvailableDto>(
                SerializerOptions,
                cancellationToken)
            ?? throw new HttpRequestException(
                "The Document Manager returned an empty result claim.");

        return new DocumentManagerResultClaim(
            dto.ResultReference,
            dto.SubmissionId,
            dto.ProcessingUnitId,
            new DocumentManagerResultScope(
                dto.Scope.Kind,
                dto.Scope.StartPhysicalPageNumber,
                dto.Scope.EndPhysicalPageNumber,
                dto.Scope.Title,
                dto.Scope.StartContentUnitIndex,
                dto.Scope.StartContentUnitId,
                dto.Scope.EndContentUnitIndex,
                dto.Scope.EndContentUnitId),
            dto.SchemaVersion,
            dto.MediaType,
            dto.ByteLength,
            dto.Sha256,
            dto.AvailableAtUtc,
            dto.ClaimToken,
            dto.ClaimExpiresAtUtc,
            new DocumentManagerSubmissionManifest(
                dto.SubmissionManifest.SubmissionId,
                dto.SubmissionManifest.Revision,
                dto.SubmissionManifest.SourceSha256,
                dto.SubmissionManifest.OriginalFileName,
                dto.SubmissionManifest.FinalizedAtUtc,
                dto.SubmissionManifest.ExpectedUnits
                    .Select(
                        unit =>
                            new DocumentManagerExpectedProcessingUnit(
                                unit.ProcessingUnitId,
                                unit.Ordinal,
                                ToScope(unit.Scope)))
                    .ToArray()));
    }

    public async Task<byte[]> ReadContentAsync(
        DocumentManagerResultClaim claim,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        using var request = CreateClaimRequest(
            HttpMethod.Get,
            $"api/manager-consumers/results/{Escape(claim.ResultReference)}/content",
            claim.ClaimToken);
        using var response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadBoundedAsync(
            response.Content,
            claim.ByteLength,
            options.MaximumResultBytes,
            "result content",
            cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentManagerVisualAssetDescriptor>>
        ListVisualAssetsAsync(
            DocumentManagerResultClaim claim,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        using var request = CreateClaimRequest(
            HttpMethod.Get,
            $"api/manager-consumers/results/{Escape(claim.ResultReference)}/visuals",
            claim.ClaimToken);
        using var response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var assets =
            await response.Content.ReadFromJsonAsync<VisualAssetDto[]>(
                SerializerOptions,
                cancellationToken)
            ?? throw new HttpRequestException(
                "The Document Manager returned an empty visual manifest.");

        return assets
            .Select(asset =>
                new DocumentManagerVisualAssetDescriptor(
                    asset.AssetId,
                    asset.MediaType,
                    asset.ByteLength,
                    asset.Sha256))
            .ToArray();
    }

    public async Task<byte[]> ReadVisualAssetAsync(
        DocumentManagerResultClaim claim,
        DocumentManagerVisualAssetDescriptor visualAsset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(visualAsset);

        using var request = CreateClaimRequest(
            HttpMethod.Get,
            $"api/manager-consumers/results/{Escape(claim.ResultReference)}/visuals/{Escape(visualAsset.AssetId)}",
            claim.ClaimToken);
        using var response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadBoundedAsync(
            response.Content,
            visualAsset.ByteLength,
            options.MaximumVisualAssetBytes,
            $"visual asset '{visualAsset.AssetId}'",
            cancellationToken);
    }

    public async Task AcknowledgeAsync(
        DocumentManagerResultClaim claim,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        using var request = CreateRequest(
            HttpMethod.Post,
            $"api/manager-consumers/results/{Escape(claim.ResultReference)}/ack");

        request.Content = JsonContent.Create(
            new AcknowledgementDto(claim.ClaimToken),
            options: SerializerOptions);

        using var response =
            await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private HttpRequestMessage CreateClaimRequest(
        HttpMethod method,
        string relativeUri,
        Guid claimToken)
    {
        var request = CreateRequest(method, relativeUri);
        request.Headers.Add(
            ClaimTokenHeader,
            claimToken.ToString("D"));
        return request;
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativeUri)
    {
        var request = new HttpRequestMessage(
            method,
            new Uri(options.BaseAddress, relativeUri));
        request.Headers.Add(
            ConsumerKeyHeader,
            options.ConsumerKey);
        request.Headers.Add(
            ConsumerIdHeader,
            options.ConsumerId);
        return request;
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        long expectedLength,
        long maximumLength,
        string description,
        CancellationToken cancellationToken)
    {
        if (expectedLength <= 0 || expectedLength > maximumLength)
        {
            throw new HttpRequestException(
                $"The advertised {description} length exceeds the configured safety limit.");
        }

        if (content.Headers.ContentLength is long contentLength &&
            contentLength != expectedLength)
        {
            throw new HttpRequestException(
                $"The HTTP {description} length does not match the Manager manifest.");
        }

        await using var source =
            await content.ReadAsStreamAsync(cancellationToken);
        using var destination =
            new MemoryStream(
                checked((int)Math.Min(expectedLength, 1024 * 1024)));

        var buffer = new byte[81920];

        while (true)
        {
            var count =
                await source.ReadAsync(
                    buffer,
                    cancellationToken);

            if (count == 0)
            {
                break;
            }

            if (destination.Length + count > maximumLength ||
                destination.Length + count > expectedLength)
            {
                throw new HttpRequestException(
                    $"The downloaded {description} exceeds its advertised length.");
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, count),
                cancellationToken);
        }

        if (destination.Length != expectedLength)
        {
            throw new HttpRequestException(
                $"The downloaded {description} is shorter than its advertised length.");
        }

        return destination.ToArray();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail =
            await ReadErrorDetailAsync(
                response.Content,
                cancellationToken);

        throw new HttpRequestException(
            $"Document Manager returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).{detail}",
            null,
            response.StatusCode);
    }

    private static async Task<string> ReadErrorDetailAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        var text =
            await content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        const int maximumCharacters = 1024;
        var sanitized =
            text.Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

        if (sanitized.Length > maximumCharacters)
        {
            sanitized = sanitized[..maximumCharacters];
        }

        return $" Response: {sanitized}";
    }

    private static string Escape(string value) =>
        Uri.EscapeDataString(value);

    private static DocumentManagerResultScope ToScope(
        ResultScopeDto scope) =>
        new(
            scope.Kind,
            scope.StartPhysicalPageNumber,
            scope.EndPhysicalPageNumber,
            scope.Title,
            scope.StartContentUnitIndex,
            scope.StartContentUnitId,
            scope.EndContentUnitIndex,
            scope.EndContentUnitId);

    private sealed record ResultAvailableDto(
        string ResultReference,
        Guid SubmissionId,
        Guid ProcessingUnitId,
        ResultScopeDto Scope,
        string SchemaVersion,
        string MediaType,
        long ByteLength,
        string Sha256,
        DateTimeOffset AvailableAtUtc,
        Guid ClaimToken,
        DateTimeOffset ClaimExpiresAtUtc,
        SubmissionManifestDto SubmissionManifest);

    private sealed record SubmissionManifestDto(
        Guid SubmissionId,
        int Revision,
        string SourceSha256,
        string OriginalFileName,
        DateTimeOffset FinalizedAtUtc,
        IReadOnlyList<ExpectedProcessingUnitDto> ExpectedUnits);

    private sealed record ExpectedProcessingUnitDto(
        Guid ProcessingUnitId,
        int Ordinal,
        ResultScopeDto Scope);

    private sealed record ResultScopeDto(
        string Kind,
        int? StartPhysicalPageNumber,
        int? EndPhysicalPageNumber,
        string? Title,
        int? StartContentUnitIndex,
        string? StartContentUnitId,
        int? EndContentUnitIndex,
        string? EndContentUnitId);

    private sealed record VisualAssetDto(
        string AssetId,
        string MediaType,
        long ByteLength,
        string Sha256);

    private sealed record AcknowledgementDto(Guid ClaimToken);
}
