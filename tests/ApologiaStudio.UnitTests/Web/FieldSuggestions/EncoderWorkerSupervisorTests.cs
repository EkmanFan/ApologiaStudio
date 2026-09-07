using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;
using ApologiaStudio.Web.FieldSuggestions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApologiaStudio.UnitTests.Web.FieldSuggestions;

/// <summary>
/// The encoder worker supervisor, driven by a scripted host and health probe.
/// </summary>
/// <remarks>
/// No Docker runs here. What is under test is the part that can quietly go
/// wrong: whether Apologia keeps working when the worker does not, and whether
/// it ever stops a worker it did not start.
/// </remarks>
public sealed class EncoderWorkerSupervisorTests
{
    #region Methods Startup

    [Fact]
    public async Task An_already_running_worker_is_reused_and_never_owned()
    {
        var host = new ScriptedHost();
        var runtime = new ScriptedRuntime { Healthy = true };

        using var supervisor = Supervisor(host, runtime);

        await RunAsync(supervisor);

        Assert.Equal(0, host.StartCalls);

        await supervisor.StopAsync(CancellationToken.None);

        // Someone else's worker is someone else's to stop.
        Assert.Equal(0, host.StopCalls);
    }

    [Fact]
    public async Task An_absent_worker_is_started_and_owned()
    {
        var host = new ScriptedHost();
        var runtime = new ScriptedRuntime { HealthyAfterStart = true };
        host.OnStart = () => runtime.Healthy = true;

        using var supervisor = Supervisor(host, runtime);

        await RunAsync(supervisor);

        Assert.Equal(1, host.StartCalls);

        await supervisor.StopAsync(CancellationToken.None);

        Assert.Equal(1, host.StopCalls);
    }

    [Theory]
    [InlineData(EncoderWorkerStartOutcome.Unsupported)]
    [InlineData(EncoderWorkerStartOutcome.Failed)]
    public async Task A_worker_that_cannot_start_does_not_stop_apologia(
        EncoderWorkerStartOutcome outcome)
    {
        // Docker absent, image absent, container refusing: all the same to the
        // application.
        var host = new ScriptedHost { Outcome = outcome };

        using var supervisor = Supervisor(host, new ScriptedRuntime());

        await RunAsync(supervisor);
        await supervisor.StopAsync(CancellationToken.None);

        Assert.Equal(0, host.StopCalls);
    }

    [Fact]
    public async Task A_worker_that_never_becomes_ready_does_not_stop_apologia()
    {
        // Started but never healthy: the capability stays unavailable and the
        // application carries on.
        var host = new ScriptedHost();

        using var supervisor = Supervisor(
            host,
            new ScriptedRuntime(),
            options => options with
            {
                StartupTimeout = TimeSpan.FromMilliseconds(50)
            });

        await RunAsync(supervisor);

        Assert.Equal(1, host.StartCalls);

        await supervisor.StopAsync(CancellationToken.None);

        // It is still ours, so it is still ours to stop.
        Assert.Equal(1, host.StopCalls);
    }

    [Fact]
    public async Task A_failing_health_probe_is_not_a_crash()
    {
        var host = new ScriptedHost();

        using var supervisor = Supervisor(
            host,
            new ScriptedRuntime { Throws = true },
            options => options with
            {
                StartupTimeout = TimeSpan.FromMilliseconds(50)
            });

        await RunAsync(supervisor);
        await supervisor.StopAsync(CancellationToken.None);
    }

    #endregion

    #region Methods Disabled

    [Fact]
    public async Task Nothing_is_started_when_auto_start_is_off()
    {
        var host = new ScriptedHost();

        using var supervisor = Supervisor(
            host,
            new ScriptedRuntime(),
            options => options with { AutoStart = false });

        await RunAsync(supervisor);
        await supervisor.StopAsync(CancellationToken.None);

        Assert.Equal(0, host.StartCalls);
        Assert.Equal(0, host.StopCalls);
    }

    [Fact]
    public async Task Nothing_is_started_when_the_configuration_is_incomplete()
    {
        var host = new ScriptedHost();

        using var supervisor = Supervisor(
            host,
            new ScriptedRuntime(),
            options => options with { ArtifactRoot = string.Empty });

        await RunAsync(supervisor);

        Assert.Equal(0, host.StartCalls);
    }

    #endregion

    #region Methods Supervision

    [Fact]
    public async Task A_worker_that_dies_is_restarted()
    {
        var host = new ScriptedHost();
        var runtime = new ScriptedRuntime();
        host.OnStart = () => runtime.Healthy = true;

        using var supervisor = Supervisor(
            host,
            runtime,
            options => options with
            {
                StartupTimeout = TimeSpan.FromSeconds(2),
                MinimumRestartBackoff = TimeSpan.FromMilliseconds(10),
                MaximumRestartBackoff = TimeSpan.FromMilliseconds(50)
            });

        await supervisor.StartAsync(CancellationToken.None);

        await WaitAsync(() => host.StartCalls == 1 && host.IsRunning);

        // The container is killed from outside.
        host.Alive = false;
        runtime.Healthy = false;
        host.OnStart = () => runtime.Healthy = true;

        await WaitAsync(() => host.StartCalls >= 2);

        // The capability came back without restarting Apologia.
        Assert.True(runtime.Healthy);

        await supervisor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_restart_that_keeps_failing_does_not_stop_apologia()
    {
        var host = new ScriptedHost();
        host.OnStart = () => host.Outcome = EncoderWorkerStartOutcome.Failed;

        using var supervisor = Supervisor(
            host,
            new ScriptedRuntime(),
            options => options with
            {
                StartupTimeout = TimeSpan.FromMilliseconds(20),
                MinimumRestartBackoff = TimeSpan.FromMilliseconds(10),
                MaximumRestartBackoff = TimeSpan.FromMilliseconds(40)
            });

        await supervisor.StartAsync(CancellationToken.None);
        await WaitAsync(() => host.StartCalls == 1 && host.IsRunning);

        // Killed from outside, and every restart from now on fails.
        host.Alive = false;

        await WaitAsync(() => host.StartCalls >= 3);

        await supervisor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_dead_worker_is_left_alone_when_restart_is_disabled()
    {
        var host = new ScriptedHost();

        using var supervisor = Supervisor(
            host,
            new ScriptedRuntime(),
            options => options with
            {
                StartupTimeout = TimeSpan.FromMilliseconds(20),
                RestartEnabled = false
            });

        await supervisor.StartAsync(CancellationToken.None);
        await WaitAsync(() => host.StartCalls == 1 && host.IsRunning);

        host.Alive = false;
        await Task.Delay(200);

        Assert.Equal(1, host.StartCalls);

        await supervisor.StopAsync(CancellationToken.None);
    }

    #endregion

    #region Methods Cancellation

    [Fact]
    public async Task Shutdown_is_honoured_while_waiting_for_readiness()
    {
        var host = new ScriptedHost();

        using var supervisor = Supervisor(
            host,
            new ScriptedRuntime(),
            options => options with
            {
                StartupTimeout = TimeSpan.FromMinutes(5)
            });

        await supervisor.StartAsync(CancellationToken.None);
        await WaitAsync(() => host.StartCalls == 1 && host.IsRunning);

        // Stops promptly rather than serving out the readiness window.
        var stopping = supervisor.StopAsync(CancellationToken.None);

        Assert.Same(
            stopping,
            await Task.WhenAny(stopping, Task.Delay(TimeSpan.FromSeconds(10))));
    }

    #endregion

    #region Methods Helpers

    private static EncoderWorkerSupervisor Supervisor(
        IEncoderWorkerHost host,
        ScriptedRuntime runtime,
        Func<EncoderWorkerOptions, EncoderWorkerOptions>? configure = null)
    {
        var options = new EncoderWorkerOptions(
            AutoStart: true,
            Image: "test-image",
            ContainerName: "test-worker",
            ArtifactRoot: "/artifacts",
            PrimaryModelPath: "/artifacts/primary",
            FallbackModelPath: "/artifacts/fallback",
            WorkerScriptPath: "/worker.py",
            Port: 5099,
            CpuThreads: 2,
            StartupTimeout: TimeSpan.FromMilliseconds(200),
            RestartEnabled: true,
            MinimumRestartBackoff: TimeSpan.FromMilliseconds(10),
            MaximumRestartBackoff: TimeSpan.FromMilliseconds(50));

        var services = new ServiceCollection();
        services.AddScoped<IEncoderInferenceRuntime>(_ => runtime);

        return new EncoderWorkerSupervisor(
            host,
            configure?.Invoke(options) ?? options,
            services.BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EncoderWorkerSupervisor>.Instance);
    }

    private static async Task RunAsync(EncoderWorkerSupervisor supervisor)
    {
        await supervisor.StartAsync(CancellationToken.None);
        await Task.Delay(300);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("The expected supervisor state was never reached.");
    }

    private sealed class ScriptedHost : IEncoderWorkerHost
    {
        public EncoderWorkerStartOutcome Outcome { get; set; } =
            EncoderWorkerStartOutcome.Started;

        public bool Alive { get; set; }

        public Action? OnStart { get; set; }

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public bool IsRunning => Alive;

        public Task<EncoderWorkerStartOutcome> StartAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCalls++;

            // Captured before the hook runs, so a hook that changes the next
            // outcome does not rewrite this one.
            var outcome = Outcome;

            if (outcome == EncoderWorkerStartOutcome.Started)
            {
                Alive = true;
            }

            OnStart?.Invoke();

            return Task.FromResult(outcome);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCalls++;
            Alive = false;
            return Task.CompletedTask;
        }
    }

    private sealed class ScriptedRuntime : IEncoderInferenceRuntime
    {
        public bool Healthy { get; set; }

        public bool HealthyAfterStart { get; init; }

        public bool Throws { get; init; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Throws)
            {
                throw new InvalidOperationException("probe exploded");
            }

            return Task.FromResult(Healthy);
        }

        public Task<EncoderInferenceResult> InferAsync(
            EncoderInferenceRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    #endregion
}
