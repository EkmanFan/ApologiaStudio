using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.Application.Abstractions.AiRuntime;
using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Evaluations.Support;

namespace ApologiaStudio.Evaluations.GenreForm.Eval6;

/// <summary>
/// EVAL-6 LLM-PER-LABEL campaign: 886 records x 24 labels, one independent
/// binary inference each.
///
/// Resumable by construction. Decisions are appended one JSON line at a time
/// and keyed by record and label, so an interrupted run continues without
/// replaying anything already recorded. The campaign manifest is written once
/// and re-checked on every resume: a run whose prompt, definitions, dataset or
/// parameters hash differently refuses to append, which is what stops
/// parameters from drifting across a multi-hour benchmark.
///
/// Scoring is deliberately NOT done here. The campaign emits decisions; a
/// separate offline scorer reads them, so every metric can be recomputed
/// without touching the model again.
/// </summary>
internal sealed class Eval6Campaign
{
    public const string Purpose = "genre-form-eval6-binary";

    private readonly Eval6Options _options;

    private readonly IStructuredGenerationRuntime _runtime;

    public Eval6Campaign(Eval6Options options)
    {
        _options = options;

        var settings = new AiRuntimeSettingsSnapshot(
            AiRuntimeSettingsSnapshot.OllamaProvider,
            LocalModelEvaluationSupport.GetBaseAddress().ToString(),
            options.Model,
            options.Model,
            30,
            options.TimeoutSeconds,
            options.KeepAlive,
            24,
            24_000,
            options.MaximumOutputTokens,
            DateTimeOffset.UtcNow,
            new Dictionary<Guid, string>());

        _runtime = new OllamaStructuredGenerationRuntime(
            new EvaluationAiRuntimeSettingsStore(settings),
            new EvaluationOllamaHttpClientFactory(),
            new NullStructuredGenerationTelemetry());
    }

    public async Task<Eval6CampaignSummary> RunAsync(CancellationToken cancellationToken)
    {
        var definitions = Eval6LabelDefinitions.Load(out var definitionsSha);
        var records = Eval6Record.Load(_options.TestSplitPath, out var datasetSha);

        var manifest = new Eval6Manifest(
            "eval6-llm-per-label",
            Eval6Prompt.Version,
            Eval6Prompt.TemplateSha256(definitions),
            definitionsSha,
            definitions.SourceSha256,
            Path.GetFullPath(_options.TestSplitPath),
            datasetSha,
            records.Count,
            Eval6Scope.MachineLabels.Count,
            _options.Model,
            _options.Temperature,
            _options.MaximumOutputTokens,
            _options.TimeoutSeconds,
            _options.MaximumAttempts,
            _options.KeepAlive);

        EnsureManifest(manifest);

        var recorded = LoadRecordedKeys(out var alreadyOk, out var alreadyUnresolved);

        var summary = new Eval6CampaignSummary
        {
            TotalDecisions = records.Count * Eval6Scope.MachineLabels.Count,
            AlreadyRecorded = recorded.Count,
            ResumedOk = alreadyOk,
            ResumedUnresolved = alreadyUnresolved
        };

        await using var writer = new StreamWriter(
            new FileStream(
                _options.DecisionsPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read));

        foreach (var record in records)
        {
            foreach (var label in Eval6Scope.MachineLabels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (recorded.Contains(Key(record.RecordId, label)))
                {
                    continue;
                }

                var decision = await DecideAsync(
                    record,
                    definitions.Labels[label],
                    cancellationToken);

                await writer.WriteLineAsync(
                    JsonSerializer.Serialize(decision).AsMemory(),
                    cancellationToken);

                // Flushed per decision: an interruption loses at most one.
                await writer.FlushAsync(cancellationToken);

                summary.Executed++;

                switch (decision.Status)
                {
                    case "ok":
                        summary.Ok++;
                        break;
                    case "invalid_json":
                        summary.Invalid++;
                        break;
                    default:
                        summary.Failed++;
                        break;
                }

                // Calibration runs stop early. A campaign leaves it unset and
                // walks the whole grid.
                if (summary.Executed >= _options.MaximumDecisions)
                {
                    return summary;
                }
            }
        }

        return summary;
    }

    private async Task<Eval6Decision> DecideAsync(
        Eval6Record record,
        Eval6LabelDefinition label,
        CancellationToken cancellationToken)
    {
        var system = Eval6Prompt.BuildSystem(label);
        var user = Eval6Prompt.BuildUser(record);

        var status = "failed";
        string? detail = null;
        bool? applicable = null;
        int? promptTokens = null;
        int? outputTokens = null;

        var startedAt = Stopwatch.GetTimestamp();
        var attempts = 0;

        while (attempts < _options.MaximumAttempts)
        {
            attempts++;

            try
            {
                var result = await _runtime.GenerateAsync(
                    new StructuredGenerationRequest(
                        Purpose,
                        system,
                        user,
                        Eval6Prompt.ResponseSchema,
                        _options.Model,
                        _options.MaximumOutputTokens),
                    cancellationToken);

                promptTokens = result.PromptTokenCount;
                outputTokens = result.OutputTokenCount;

                using var document = JsonDocument.Parse(result.Json);

                if (document.RootElement.TryGetProperty("applicable", out var value) &&
                    (value.ValueKind == JsonValueKind.True ||
                     value.ValueKind == JsonValueKind.False))
                {
                    applicable = value.ValueKind == JsonValueKind.True;
                    status = "ok";
                    detail = null;
                    break;
                }

                status = "invalid_json";
                detail = "the response carries no boolean 'applicable'";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException exception)
            {
                status = "invalid_json";
                detail = exception.GetType().Name;
            }
            catch (Exception exception)
            {
                status = "failed";
                detail = $"{exception.GetType().Name}: {exception.Message}";
            }
        }

        // A failed or invalid decision is never silently recorded as false. It
        // is excluded from accuracy and reported on its own, exactly as
        // contract failures were in EVAL-1 through EVAL-5.
        return new Eval6Decision(
            record.RecordId,
            label.Label,
            applicable,
            status,
            detail,
            attempts,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            promptTokens,
            outputTokens,
            _options.Model,
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }

    private void EnsureManifest(Eval6Manifest manifest)
    {
        var serialized = JsonSerializer.Serialize(
            manifest,
            new JsonSerializerOptions { WriteIndented = true });

        if (!File.Exists(_options.ManifestPath))
        {
            File.WriteAllText(_options.ManifestPath, serialized);
            return;
        }

        var existing = File.ReadAllText(_options.ManifestPath);

        if (!string.Equals(existing.Trim(), serialized.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The campaign manifest differs from the one this result set was " +
                "started with: prompt, definitions, dataset or parameters " +
                "changed. Start a new output directory rather than mixing two " +
                "campaigns in one file.");
        }
    }

    private HashSet<string> LoadRecordedKeys(out int ok, out int unresolved)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        ok = 0;
        unresolved = 0;

        if (!File.Exists(_options.DecisionsPath))
        {
            return keys;
        }

        foreach (var line in File.ReadLines(_options.DecisionsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Eval6Decision? decision;

            try
            {
                decision = JsonSerializer.Deserialize<Eval6Decision>(line);
            }
            catch (JsonException)
            {
                // A torn final line from a hard interruption: ignored, so the
                // decision is simply taken again.
                continue;
            }

            if (decision is null)
            {
                continue;
            }

            if (decision.Status == "ok")
            {
                ok++;
            }
            else
            {
                unresolved++;

                if (_options.RetryUnresolved)
                {
                    continue;
                }
            }

            keys.Add(Key(decision.RecordId, decision.Label));
        }

        return keys;
    }

    private static string Key(string recordId, string label) => $"{recordId}::{label}";
}

internal sealed record Eval6Options(
    string TestSplitPath,
    string OutputDirectory,
    string Model = "qwen3.8:27b",
    double Temperature = 0.2,
    int MaximumOutputTokens = 64,
    int TimeoutSeconds = 120,
    int MaximumAttempts = 3,
    string KeepAlive = "30m",
    bool RetryUnresolved = false,
    int? MaximumDecisions = null)
{
    public string DecisionsPath => Path.Combine(OutputDirectory, "decisions.jsonl");

    public string ManifestPath => Path.Combine(OutputDirectory, "campaign-manifest.json");
}

/// <summary>
/// Written once and re-checked on every resume. Temperature is recorded rather
/// than sent: the runtime pins it at 0.2, so the manifest states the value the
/// campaign actually ran under instead of one it could silently change.
/// </summary>
internal sealed record Eval6Manifest(
    string Campaign,
    string PromptVersion,
    string PromptTemplateSha256,
    string DefinitionsSha256,
    string DefinitionsSourceSha256,
    string TestSplitPath,
    string TestSplitSha256,
    int RecordCount,
    int LabelCount,
    string Model,
    double Temperature,
    int MaximumOutputTokens,
    int TimeoutSeconds,
    int MaximumAttempts,
    string KeepAlive);

internal sealed record Eval6Decision(
    string RecordId,
    string Label,
    bool? Applicable,
    string Status,
    string? Detail,
    int Attempts,
    double LatencyMilliseconds,
    int? PromptTokenCount,
    int? OutputTokenCount,
    string Model,
    string RecordedAtUtc);

internal sealed class Eval6CampaignSummary
{
    public int TotalDecisions { get; set; }

    public int AlreadyRecorded { get; set; }

    public int ResumedOk { get; set; }

    public int ResumedUnresolved { get; set; }

    public int Executed { get; set; }

    public int Ok { get; set; }

    public int Invalid { get; set; }

    public int Failed { get; set; }
}

internal sealed class NullStructuredGenerationTelemetry : IStructuredGenerationTelemetry
{
    public void GenerationStarted(StructuredGenerationStartedObservation observation)
    {
    }

    public void GenerationCompleted(StructuredGenerationCompletedObservation observation)
    {
    }

    public void GenerationFailed(StructuredGenerationFailedObservation observation)
    {
    }
}
