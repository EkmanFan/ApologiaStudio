using System.Diagnostics;
using System.ComponentModel;

namespace ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

/// <summary>
/// How the encoder worker is hosted.
/// </summary>
/// <remarks>
/// Only the composition root supervises this. Neither
/// <c>IFieldSuggestionProvider</c> nor <c>IEncoderInferenceRuntime</c> depends
/// on it: a suggestion capability must not know how its runtime was started,
/// or an externally managed worker would stop being a supported case.
/// </remarks>
public sealed record EncoderWorkerOptions(
    bool AutoStart,
    string Image,
    string ContainerName,
    string ArtifactRoot,
    string PrimaryModelPath,
    string FallbackModelPath,
    string WorkerScriptPath,
    int Port,
    int CpuThreads,
    TimeSpan StartupTimeout,
    bool RestartEnabled,
    TimeSpan MinimumRestartBackoff,
    TimeSpan MaximumRestartBackoff)
{
    public static EncoderWorkerOptions Disabled { get; } =
        new(
            AutoStart: false,
            Image: string.Empty,
            ContainerName: string.Empty,
            ArtifactRoot: string.Empty,
            PrimaryModelPath: string.Empty,
            FallbackModelPath: string.Empty,
            WorkerScriptPath: string.Empty,
            Port: 0,
            CpuThreads: 8,
            StartupTimeout: TimeSpan.FromSeconds(60),
            RestartEnabled: false,
            MinimumRestartBackoff: TimeSpan.FromSeconds(5),
            MaximumRestartBackoff: TimeSpan.FromMinutes(2));

    /// <summary>
    /// Gets whether auto-start has everything it needs. A missing image, model
    /// root or worker source is a configuration gap, not a runtime failure.
    /// </summary>
    public bool CanAutoStart =>
        AutoStart &&
        !string.IsNullOrWhiteSpace(Image) &&
        !string.IsNullOrWhiteSpace(ContainerName) &&
        !string.IsNullOrWhiteSpace(ArtifactRoot) &&
        !string.IsNullOrWhiteSpace(PrimaryModelPath) &&
        !string.IsNullOrWhiteSpace(FallbackModelPath) &&
        !string.IsNullOrWhiteSpace(WorkerScriptPath) &&
        Port > 0;
}

/// <summary>
/// Outcome of an attempt to start the worker.
/// </summary>
public enum EncoderWorkerStartOutcome
{
    /// <summary>The worker process was started and is owned by this host.</summary>
    Started = 0,

    /// <summary>The hosting mechanism is not usable here.</summary>
    Unsupported = 1,

    /// <summary>The mechanism is present and starting the worker failed.</summary>
    Failed = 2
}

/// <summary>
/// Starts and stops the encoder worker.
/// </summary>
/// <remarks>
/// One mechanism, one implementation. This is not a process-supervision
/// framework and must not grow into one.
/// </remarks>
public interface IEncoderWorkerHost
{
    /// <summary>Gets whether the worker this host started is still alive.</summary>
    bool IsRunning { get; }

    Task<EncoderWorkerStartOutcome> StartAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops the worker this host started. Stopping one it did not start is
    /// never its business.
    /// </summary>
    Task StopAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Runs the encoder worker as a container.
/// </summary>
/// <remarks>
/// Arguments are built as a list and handed to the process directly: no shell,
/// no string concatenation, and therefore nothing to inject into. The container
/// gets no GPU device, no elevated privilege and no Docker socket — it loads
/// two models and answers on a loopback port.
///
/// The worker source is written to the container's stdin rather than mounted,
/// which keeps the qualified P3-03 behaviour and avoids relabelling a source
/// tree.
/// </remarks>
public sealed class DockerEncoderWorkerHost(
    EncoderWorkerOptions options)
    : IEncoderWorkerHost, IDisposable
{
    #region Variables and Constants

    private readonly SemaphoreSlim _gate = new(1, 1);

    private Process? _worker;

    #endregion

    #region Properties

    /// <inheritdoc />
    public bool IsRunning => _worker is { HasExited: false };

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<EncoderWorkerStartOutcome> StartAsync(
        CancellationToken cancellationToken)
    {
        if (!options.CanAutoStart)
        {
            return EncoderWorkerStartOutcome.Unsupported;
        }

        if (!File.Exists(options.WorkerScriptPath) ||
            !Directory.Exists(options.ArtifactRoot))
        {
            return EncoderWorkerStartOutcome.Unsupported;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (IsRunning)
            {
                return EncoderWorkerStartOutcome.Started;
            }

            if (await RunAsync(["image", "inspect", options.Image], cancellationToken)
                is not 0)
            {
                return EncoderWorkerStartOutcome.Unsupported;
            }

            // A container left behind by an earlier run would hold the port.
            await RunAsync(
                ["rm", "--force", options.ContainerName],
                cancellationToken);

            return await LaunchAsync(cancellationToken);
        }
        catch (Win32Exception)
        {
            // No docker executable on this machine.
            return EncoderWorkerStartOutcome.Unsupported;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_worker is null)
            {
                return;
            }

            try
            {
                await RunAsync(
                    ["stop", "--time", "5", options.ContainerName],
                    cancellationToken);

                await _worker.WaitForExitAsync(cancellationToken);
            }
            catch (Exception exception) when (
                exception is Win32Exception or InvalidOperationException)
            {
                // Already gone; nothing left to stop.
            }
            finally
            {
                _worker.Dispose();
                _worker = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _worker?.Dispose();
        _gate.Dispose();
    }

    #endregion

    #region Methods Process

    private async Task<EncoderWorkerStartOutcome> LaunchAsync(
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in RunArguments())
        {
            start.ArgumentList.Add(argument);
        }

        var process = Process.Start(start);

        if (process is null)
        {
            return EncoderWorkerStartOutcome.Failed;
        }

        // The worker source is the container's program, read from stdin. Closing
        // the stream is what makes the interpreter run it.
        var source = await File.ReadAllTextAsync(
            options.WorkerScriptPath,
            cancellationToken);

        await process.StandardInput.WriteAsync(source);
        await process.StandardInput.FlushAsync(cancellationToken);
        process.StandardInput.Close();

        // Output is drained so the worker is never blocked on a full pipe. It
        // is discarded: it can contain nothing useful and must never carry
        // document content into the application log.
        _ = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        _ = process.StandardError.ReadToEndAsync(CancellationToken.None);

        _worker = process;

        return EncoderWorkerStartOutcome.Started;
    }

    /// <summary>
    /// The container contract qualified in P3-03: CPU only, no visible GPU, two
    /// resident models, loopback port, artifacts read-only.
    /// </summary>
    private IEnumerable<string> RunArguments()
    {
        yield return "run";
        yield return "--rm";
        yield return "--interactive";
        yield return "--name";
        yield return options.ContainerName;

        yield return "--publish";
        yield return $"127.0.0.1:{options.Port}:{options.Port}";

        foreach (var variable in new[]
                 {
                     "CUDA_VISIBLE_DEVICES=",
                     "HIP_VISIBLE_DEVICES=",
                     "ROCR_VISIBLE_DEVICES=",
                     "TOKENIZERS_PARALLELISM=false",
                     "PYTHONUNBUFFERED=1",
                     $"OMP_NUM_THREADS={options.CpuThreads}",
                     $"MKL_NUM_THREADS={options.CpuThreads}",
                     $"OPENBLAS_NUM_THREADS={options.CpuThreads}",
                     $"ENCODER_PRIMARY_MODEL_PATH={options.PrimaryModelPath}",
                     $"ENCODER_FALLBACK_MODEL_PATH={options.FallbackModelPath}",
                     $"ENCODER_WORKER_PORT={options.Port}",
                     $"ENCODER_CPU_THREADS={options.CpuThreads}"
                 })
        {
            yield return "--env";
            yield return variable;
        }

        yield return "--volume";
        yield return $"{options.ArtifactRoot}:/artifacts:ro";

        yield return options.Image;
        yield return "python3";
        yield return "-";
    }

    private static async Task<int?> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);

        if (process is null)
        {
            return null;
        }

        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode;
    }

    #endregion
}
