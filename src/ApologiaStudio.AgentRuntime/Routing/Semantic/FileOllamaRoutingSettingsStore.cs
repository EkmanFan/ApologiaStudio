using System.Text.Json;

namespace ApologiaStudio.AgentRuntime.Routing.Semantic;

public sealed class FileOllamaRoutingSettingsStore
    : IOllamaRoutingSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private OllamaRoutingSettings _current;

    public FileOllamaRoutingSettingsStore(
        string filePath,
        OllamaRoutingSettings defaults)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "A routing-settings file path is required.",
                nameof(filePath));
        }

        ArgumentNullException.ThrowIfNull(defaults);

        _filePath = Path.GetFullPath(filePath);
        OllamaRoutingSettingsValidator.ToOptions(defaults);

        _current = File.Exists(_filePath)
            ? LoadPersistedSettings(_filePath)
            : defaults;
    }

    public OllamaRoutingSettings Current =>
        Volatile.Read(ref _current);

    public async Task SaveAsync(
        OllamaRoutingSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized =
            NormalizeAndValidate(settings);

        await _writeLock
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        string? temporaryPath = null;

        try
        {
            var directory =
                Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException(
                    "The routing-settings file has no parent directory.");

            Directory.CreateDirectory(directory);

            temporaryPath =
                Path.Combine(
                    directory,
                    $".{Path.GetFileName(_filePath)}." +
                    $"{Guid.NewGuid():N}.tmp");

            var json =
                JsonSerializer.Serialize(
                    normalized,
                    SerializerOptions);

            await File.WriteAllTextAsync(
                    temporaryPath,
                    json,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                temporaryPath,
                _filePath,
                overwrite: true);

            temporaryPath = null;
            Volatile.Write(ref _current, normalized);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                File.Delete(temporaryPath);
            }

            _writeLock.Release();
        }
    }

    private static OllamaRoutingSettings LoadPersistedSettings(
        string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);

            var settings =
                JsonSerializer.Deserialize<OllamaRoutingSettings>(
                    json,
                    SerializerOptions)
                ?? throw new InvalidOperationException(
                    "The persisted Ollama routing settings are empty.");

            return NormalizeAndValidate(settings);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The persisted Ollama routing settings at " +
                $"'{filePath}' are invalid JSON.",
                exception);
        }
    }

    private static OllamaRoutingSettings NormalizeAndValidate(
        OllamaRoutingSettings settings)
    {
        var options =
            OllamaRoutingSettingsValidator.ToOptions(settings);

        return new OllamaRoutingSettings(
            options.BaseAddress.ToString(),
            options.Model,
            checked((int)options.RequestTimeout.TotalSeconds),
            options.KeepAlive);
    }
}
