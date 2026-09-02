using ApologiaStudio.Infrastructure.Knowledge.DocumentProcessing;

namespace ApologiaStudio.DocumentManagerConsumer;

internal sealed record DocumentManagerConsumerSettings(
    bool RunContinuously,
    string KnowledgeConnectionString,
    DocumentManagerHttpOptions Manager,
    TimeSpan PollInterval,
    TimeSpan RequestTimeout)
{
    public static DocumentManagerConsumerSettings Parse(string[] args)
    {
        if (args.Length != 1 ||
            (args[0] is not "consume-once" and not "run"))
        {
            throw new ArgumentException(
                "Expected exactly one command: consume-once or run.");
        }

        var connectionString = RequireEnvironment(
            "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");
        var consumerKey =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_KEY")
            ?? Environment.GetEnvironmentVariable(
                "DPE_MANAGER_CONSUMER_API_KEY")
            ?? throw new InvalidOperationException(
                "APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_KEY must be defined.");
        var baseAddressText =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_DOCUMENT_MANAGER_URL")
            ?? "http://127.0.0.1:5080/";
        var consumerId =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_DOCUMENT_MANAGER_CONSUMER_ID")
            ?? "apologia-studio";

        if (!Uri.TryCreate(
                baseAddressText,
                UriKind.Absolute,
                out var baseAddress))
        {
            throw new InvalidOperationException(
                "APOLOGIASTUDIO_DOCUMENT_MANAGER_URL is not a valid absolute URI.");
        }

        var pollSeconds = ReadPositiveInt32(
            "APOLOGIASTUDIO_DOCUMENT_MANAGER_POLL_SECONDS",
            5);
        var timeoutSeconds = ReadPositiveInt32(
            "APOLOGIASTUDIO_DOCUMENT_MANAGER_TIMEOUT_SECONDS",
            120);
        var maximumResultBytes = ReadPositiveInt64(
            "APOLOGIASTUDIO_DOCUMENT_MANAGER_MAX_RESULT_BYTES",
            DocumentManagerHttpOptions.DefaultMaximumResultBytes);
        var maximumVisualBytes = ReadPositiveInt64(
            "APOLOGIASTUDIO_DOCUMENT_MANAGER_MAX_VISUAL_BYTES",
            DocumentManagerHttpOptions.DefaultMaximumVisualAssetBytes);

        return new DocumentManagerConsumerSettings(
            args[0] == "run",
            connectionString,
            new DocumentManagerHttpOptions(
                baseAddress,
                consumerKey,
                consumerId,
                maximumResultBytes,
                maximumVisualBytes),
            TimeSpan.FromSeconds(pollSeconds),
            TimeSpan.FromSeconds(timeoutSeconds));
    }

    private static string RequireEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"{name} must be defined.")
            : value;
    }

    private static int ReadPositiveInt32(
        string name,
        int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new InvalidOperationException(
                $"{name} must be a positive integer.");
    }

    private static long ReadPositiveInt64(
        string name,
        long defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return long.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new InvalidOperationException(
                $"{name} must be a positive integer.");
    }
}
