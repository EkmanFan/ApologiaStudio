using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;
using ApologiaStudio.Web.FieldSuggestions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApologiaStudio.IntegrationTests.FieldSuggestions;

/// <summary>
/// The real worker lifecycle, driven by the production supervisor and host.
/// </summary>
/// <remarks>
/// Opt-in: it starts and stops real containers. It exercises the classes the
/// application composes — <see cref="DockerEncoderWorkerHost"/> and
/// <see cref="EncoderWorkerSupervisor"/> — rather than a stand-in, because what
/// is in doubt is the interaction with Docker, not the state machine.
/// </remarks>
public sealed class EncoderWorkerLifecycleSmokeTests
{
    #region Methods

    [Fact]
    public async Task Apologia_starts_supervises_and_stops_a_worker_it_owns()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        await RemoveContainerAsync();

        var options = WorkerOptions();
        using var host = new DockerEncoderWorkerHost(options);
        using var httpClient = new HttpClient();
        var runtime = Runtime(httpClient);

        var supervisor = Supervisor(host, options, runtime);

        Assert.False(
            await runtime.IsAvailableAsync(CancellationToken.None),
            "a worker was already running; this case needs none");

        await supervisor.StartAsync(CancellationToken.None);

        // Started automatically and became usable, with no manual step.
        Assert.True(
            await WaitAsync(
                () => runtime.IsAvailableAsync(CancellationToken.None),
                TimeSpan.FromMinutes(2)),
            "the supervisor never made the worker ready");

        // A real suggestion travels the whole path.
        var batch = await new EncoderBackedFieldSuggestionProvider(
                new GenreFormInferencePlan(runtime))
            .GetSuggestionsAsync(
                new FieldSuggestionRequest(
                    Guid.NewGuid(),
                    new FieldSuggestionEvidence("Sermons sur la grâce", null)),
                [ApologiaFieldId.GenreForm],
                CancellationToken.None);

        Assert.Equal(
            FieldSuggestionStatus.Succeeded,
            Assert.Single(batch.Results).Status);

        // The container is killed from outside, as an operator might.
        await RemoveContainerAsync();

        Assert.True(
            await WaitAsync(
                () => runtime.IsAvailableAsync(CancellationToken.None),
                TimeSpan.FromMinutes(3)),
            "the supervisor did not bring the worker back");

        await supervisor.StopAsync(CancellationToken.None);

        // A worker Apologia started is one Apologia stops.
        Assert.False(await ContainerExistsAsync());
    }

    [Fact]
    public async Task An_externally_started_worker_is_reused_and_left_running()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        await RemoveContainerAsync();

        var options = WorkerOptions();
        using var httpClient = new HttpClient();
        var runtime = Runtime(httpClient);

        // Started the way an operator would, with no Apologia label on it.
        using var external = await StartUnlabelledWorkerAsync(options);

        try
        {
            Assert.True(
                await WaitAsync(
                    () => runtime.IsAvailableAsync(CancellationToken.None),
                    TimeSpan.FromMinutes(2)),
                "the external worker never became ready");

            Assert.False(await IsManagedAsync());

            using var supervised = new DockerEncoderWorkerHost(options);
            var supervisor = Supervisor(supervised, options, runtime);

            await supervisor.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Not adopted and not duplicated: it carries no Apologia label.
            Assert.False(
                await supervised.IsRunningAsync(CancellationToken.None));

            await supervisor.StopAsync(CancellationToken.None);

            // And the worker Apologia found is still there.
            Assert.True(await ContainerExistsAsync());
            Assert.True(await runtime.IsAvailableAsync(CancellationToken.None));
        }
        finally
        {
            await RemoveContainerAsync();
        }
    }

    [Fact]
    public async Task A_worker_that_outlives_a_killed_apologia_is_taken_back()
    {
        if (!LiveEncoderIntegrationGate.IsEnabled())
        {
            return;
        }

        await RemoveContainerAsync();

        var options = WorkerOptions();
        using var httpClient = new HttpClient();
        var runtime = Runtime(httpClient);

        // A first Apologia starts the worker.
        var first = new DockerEncoderWorkerHost(options);
        var firstSupervisor = Supervisor(first, options, runtime);

        await firstSupervisor.StartAsync(CancellationToken.None);

        Assert.True(
            await WaitAsync(
                () => runtime.IsAvailableAsync(CancellationToken.None),
                TimeSpan.FromMinutes(2)),
            "the first supervisor never made the worker ready");

        Assert.True(await IsManagedAsync());

        // It is killed without ever shutting down: no StopAsync, no cleanup.
        first.Dispose();

        // A second Apologia starts and finds a worker already answering.
        using var second = new DockerEncoderWorkerHost(options);
        var secondSupervisor = Supervisor(second, options, runtime);

        await secondSupervisor.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Taken back rather than treated as a stranger's, and not duplicated.
        Assert.True(await second.IsRunningAsync(CancellationToken.None));

        await secondSupervisor.StopAsync(CancellationToken.None);

        // The ownership was not lost: the container is gone.
        Assert.False(await ContainerExistsAsync());
    }

    #endregion

    #region Methods Helpers

    private static EncoderWorkerSupervisor Supervisor(
        IEncoderWorkerHost host,
        EncoderWorkerOptions options,
        IEncoderInferenceRuntime runtime)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => runtime);

        return new EncoderWorkerSupervisor(
            host,
            options,
            services.BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EncoderWorkerSupervisor>.Instance);
    }

    private static IEncoderInferenceRuntime Runtime(HttpClient httpClient)
    {
        var options = new EncoderInferenceOptions(
            LiveEncoderIntegrationGate.Endpoint(),
            TimeSpan.FromSeconds(60));

        httpClient.Timeout = options.Timeout;

        return new HttpEncoderInferenceRuntime(httpClient, options);
    }

    private static EncoderWorkerOptions WorkerOptions() =>
        new(
            AutoStart: true,
            Image: Environment.GetEnvironmentVariable("ENCODER_WORKER_IMAGE")
                   ?? "lcgft-ml:rocm7.2.4-mdeberta-v1",
            ContainerName: ContainerName,
            ArtifactRoot: Environment.GetEnvironmentVariable("ENCODER_ARTIFACT_ROOT")
                          ?? "/mnt/SharedDrive/Spike Encoder/Spike Encoder V2",
            PrimaryModelPath:
            "/artifacts/models/xlm-roberta-large/gate-e-v1/best-model",
            FallbackModelPath:
            "/artifacts/models/mdeberta-v3-base/gate-e-v1/best-model",
            WorkerScriptPath: WorkerScriptPath(),
            Port: LiveEncoderIntegrationGate.Endpoint().Port,
            CpuThreads: 8,
            StartupTimeout: TimeSpan.FromSeconds(90),
            RestartEnabled: true,
            MinimumRestartBackoff: TimeSpan.FromSeconds(2),
            MaximumRestartBackoff: TimeSpan.FromSeconds(20));

    private const string ContainerName = "apologia-encoder-worker-smoke";

    /// <summary>
    /// Walks up to the repository copy of the worker source.
    /// </summary>
    private static string WorkerScriptPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "tools",
                "encoder-worker",
                "encoder_worker.py");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return string.Empty;
    }

    private static async Task<bool> WaitAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        return false;
    }

    private static async Task RemoveContainerAsync()
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("docker")
            {
                ArgumentList = { "rm", "--force", ContainerName },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

        if (process is not null)
        {
            await process.WaitForExitAsync();
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Whether the running container carries the Apologia ownership label.
    /// </summary>
    /// <summary>
    /// Starts a worker the way something other than Apologia would: same
    /// container, no ownership label.
    /// </summary>
    private static async Task<System.Diagnostics.Process> StartUnlabelledWorkerAsync(
        EncoderWorkerOptions options)
    {
        var start = new System.Diagnostics.ProcessStartInfo("docker")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in new[]
                 {
                     "run", "--rm", "--interactive",
                     "--name", options.ContainerName,
                     "--publish", $"127.0.0.1:{options.Port}:{options.Port}",
                     "--env", "CUDA_VISIBLE_DEVICES=",
                     "--env", "HIP_VISIBLE_DEVICES=",
                     "--env", "ROCR_VISIBLE_DEVICES=",
                     "--env", "TOKENIZERS_PARALLELISM=false",
                     "--env", "PYTHONUNBUFFERED=1",
                     "--env", $"ENCODER_PRIMARY_MODEL_PATH={options.PrimaryModelPath}",
                     "--env", $"ENCODER_FALLBACK_MODEL_PATH={options.FallbackModelPath}",
                     "--env", $"ENCODER_WORKER_PORT={options.Port}",
                     "--env", $"ENCODER_CPU_THREADS={options.CpuThreads}",
                     "--volume", $"{options.ArtifactRoot}:/artifacts:ro",
                     options.Image, "python3", "-"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        var process = System.Diagnostics.Process.Start(start)!;

        await process.StandardInput.WriteAsync(
            await File.ReadAllTextAsync(options.WorkerScriptPath));
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();

        return process;
    }

    private static async Task<bool> IsManagedAsync()
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("docker")
            {
                ArgumentList =
                {
                    "ps", "--format", "{{.Names}}",
                    "--filter", $"name={ContainerName}",
                    "--filter", $"label={DockerEncoderWorkerHost.ManagedLabel}"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

        if (process is null)
        {
            return false;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output.Contains(ContainerName, StringComparison.Ordinal);
    }

    private static async Task<bool> ContainerExistsAsync()
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("docker")
            {
                ArgumentList =
                {
                    "ps", "--quiet", "--filter", $"name={ContainerName}"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

        if (process is null)
        {
            return false;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return !string.IsNullOrWhiteSpace(output);
    }

    #endregion
}
