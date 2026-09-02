namespace ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

public sealed record DocumentManagerHttpOptions
{
    public const long DefaultMaximumResultBytes = 128L * 1024 * 1024;
    public const long DefaultMaximumVisualAssetBytes = 128L * 1024 * 1024;

    public Uri BaseAddress { get; }
    public string ConsumerKey { get; }
    public string ConsumerId { get; }
    public long MaximumResultBytes { get; }
    public long MaximumVisualAssetBytes { get; }

    public DocumentManagerHttpOptions(
        Uri baseAddress,
        string consumerKey,
        string consumerId,
        long maximumResultBytes = DefaultMaximumResultBytes,
        long maximumVisualAssetBytes = DefaultMaximumVisualAssetBytes)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        if (!baseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "The Document Manager base address must be absolute.",
                nameof(baseAddress));
        }

        if (!string.Equals(
                baseAddress.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) &&
            !(string.Equals(
                  baseAddress.Scheme,
                  Uri.UriSchemeHttp,
                  StringComparison.OrdinalIgnoreCase) &&
              baseAddress.IsLoopback))
        {
            throw new ArgumentException(
                "The Document Manager must use HTTPS unless it is reached through loopback HTTP.",
                nameof(baseAddress));
        }

        if (string.IsNullOrWhiteSpace(consumerKey) ||
            consumerKey.Length < 32 ||
            ContainsNewLine(consumerKey))
        {
            throw new ArgumentException(
                "The Document Manager consumer key must contain at least 32 characters and no line breaks.",
                nameof(consumerKey));
        }

        if (string.IsNullOrWhiteSpace(consumerId) ||
            ContainsNewLine(consumerId))
        {
            throw new ArgumentException(
                "The Document Manager consumer ID cannot be empty or contain line breaks.",
                nameof(consumerId));
        }

        if (maximumResultBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResultBytes));
        }

        if (maximumVisualAssetBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumVisualAssetBytes));
        }

        BaseAddress = EnsureTrailingSlash(baseAddress);
        ConsumerKey = consumerKey.Trim();
        ConsumerId = consumerId.Trim();
        MaximumResultBytes = maximumResultBytes;
        MaximumVisualAssetBytes = maximumVisualAssetBytes;
    }

    private static bool ContainsNewLine(string value) =>
        value.Contains('\r') || value.Contains('\n');

    private static Uri EnsureTrailingSlash(Uri value)
    {
        var text = value.AbsoluteUri;

        return text.EndsWith("/", StringComparison.Ordinal)
            ? value
            : new Uri(text + '/', UriKind.Absolute);
    }
}
