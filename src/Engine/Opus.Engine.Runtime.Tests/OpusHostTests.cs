using FluentAssertions;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Sdl3;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Runtime.Tests;

public sealed class OpusHostTests
{
    [Fact]
    public void Start_opens_configured_window_before_application_callback()
    {
        var app = new RecordingApplication();
        using var window = new RecordingWindowService();
        using var host = new OpusHost(
            app,
            new OpusHostOptions { Window = WindowOptions.Default("Opus Runtime") },
            window);

        host.Start();

        window.OpenCount.Should().Be(1);
        window.IsOpen.Should().BeTrue();
        app.StartedWindowOpen.Should().BeTrue();
        host.State.Should().Be(OpusHostState.Running);
    }

    [Fact]
    public void Step_drains_fixed_ticks_before_render()
    {
        var app = new RecordingApplication();
        using var host = new OpusHost(app);

        host.Start();
        host.Step(TimeSpan.FromSeconds(2.0 / GameTime.DefaultTickRateHz));

        app.FixedTicks.Should().Equal(0, 1);
        app.RenderFrames.Should().ContainSingle();
        app.RenderFrames[0].Time.Tick.Value.Should().Be(2);
        app.RenderFrames[0].FixedTicksExecuted.Should().Be(2);
        host.FixedTicksExecuted.Should().Be(2);
    }

    [Fact]
    public void Step_preserves_fractional_alpha_when_frame_is_shorter_than_one_fixed_tick()
    {
        var app = new RecordingApplication();
        using var host = new OpusHost(app);

        host.Start();
        host.Step(TimeSpan.FromSeconds(0.5 / GameTime.DefaultTickRateHz));

        app.FixedTicks.Should().BeEmpty();
        app.RenderFrames.Should().ContainSingle();
        app.RenderFrames[0].Time.Tick.Value.Should().Be(0);
        app.RenderFrames[0].InterpolationAlpha.Should().BeApproximately(0.5, 0.0001);
    }

    [Fact]
    public void Pause_stops_fixed_ticks_but_can_keep_rendering()
    {
        var app = new RecordingApplication();
        using var host = new OpusHost(app);

        host.Start();
        host.Pause();
        host.Step(TimeSpan.FromSeconds(4.0 / GameTime.DefaultTickRateHz));
        host.Resume();
        host.Step(TimeSpan.FromSeconds(1.0 / GameTime.DefaultTickRateHz));

        app.PausedCount.Should().Be(1);
        app.ResumedCount.Should().Be(1);
        app.FixedTicks.Should().Equal(0);
        app.RenderFrames.Should().HaveCount(2);
        app.RenderFrames[0].FixedTicksExecuted.Should().Be(0);
        app.RenderFrames[1].FixedTicksExecuted.Should().Be(1);
    }

    [Fact]
    public void Step_caps_fixed_tick_catchup_and_records_dropped_time()
    {
        var app = new RecordingApplication();
        using var host = new OpusHost(
            app,
            new OpusHostOptions { MaxFixedTicksPerFrame = 2 });

        host.Start();
        host.Step(TimeSpan.FromSeconds(10.0 / GameTime.DefaultTickRateHz));
        host.Step(TimeSpan.Zero);

        app.FixedTicks.Should().Equal(0, 1);
        app.RenderFrames.Should().HaveCount(2);
        host.DroppedFixedTime.Should().BeCloseTo(
            TimeSpan.FromSeconds(8.0 / GameTime.DefaultTickRateHz),
            TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Window_close_request_shuts_down_once_on_next_step()
    {
        var app = new RecordingApplication();
        using var window = new RecordingWindowService { RequestCloseOnNextPoll = true };
        using var host = new OpusHost(
            app,
            new OpusHostOptions { Window = WindowOptions.Default("Opus Runtime") },
            window);

        host.Start();
        var keepRunning = host.Step(TimeSpan.Zero);
        host.Stop();

        keepRunning.Should().BeFalse();
        host.State.Should().Be(OpusHostState.Stopped);
        app.StoppingCount.Should().Be(1);
        window.CloseCount.Should().Be(1);
    }

    [Fact]
    public void Lifecycle_shutdown_request_stops_host_on_next_step()
    {
        var app = new RecordingApplication();
        var lifecycle = new RecordingLifecycleService();
        using var host = new OpusHost(app, lifecycle: lifecycle);

        host.Start();
        lifecycle.RaiseShuttingDown();
        var keepRunning = host.Step(TimeSpan.Zero);

        keepRunning.Should().BeFalse();
        host.State.Should().Be(OpusHostState.Stopped);
        app.StoppingCount.Should().Be(1);
    }

    [Fact]
    public void Noop_application_smoke_opens_steps_and_closes_without_game_code()
    {
        using var window = new RecordingWindowService();
        using var host = new OpusHost(
            new OpusNoopApplication(),
            new OpusHostOptions { Window = WindowOptions.Default("Opus Noop Smoke") },
            window);

        host.Start();
        host.Step(TimeSpan.Zero);
        host.Stop();

        window.OpenCount.Should().Be(1);
        window.CloseCount.Should().Be(1);
        host.FrameIndex.Should().Be(1);
        host.State.Should().Be(OpusHostState.Stopped);
    }

    [SkippableFact]
    public void Noop_application_can_drive_real_sdl_window_without_game_code()
    {
        Skip.IfNot(CanInitSdlVideo(), "SDL video subsystem not available on this host.");

        using var window = new SdlWindowService();
        using var host = new OpusHost(
            new OpusNoopApplication(),
            new OpusHostOptions
            {
                Window = new WindowOptions(
                    "Opus Runtime SDL Smoke",
                    320,
                    180,
                    Resizable: false,
                    VSync: false,
                    WindowMode.Windowed),
            },
            window);

        host.Start();
        var keepRunning = host.Step(TimeSpan.FromSeconds(1.0 / GameTime.DefaultTickRateHz));
        host.Stop();

        keepRunning.Should().BeTrue();
        window.IsOpen.Should().BeFalse();
        host.FrameIndex.Should().Be(1);
        host.State.Should().Be(OpusHostState.Stopped);
    }

    private static bool CanInitSdlVideo()
    {
        try
        {
            using var probe = new SdlWindowService();
            probe.Open(new WindowOptions(
                "Opus Runtime SDL Probe",
                16,
                16,
                Resizable: false,
                VSync: false,
                WindowMode.Windowed));

            return probe.IsOpen;
        }
        catch
        {
            return false;
        }
    }

    private sealed class RecordingApplication : IOpusApplication
    {
        public List<long> FixedTicks { get; } = new();

        public List<OpusRenderFrame> RenderFrames { get; } = new();

        public bool StartedWindowOpen { get; private set; }

        public int PausedCount { get; private set; }

        public int ResumedCount { get; private set; }

        public int StoppingCount { get; private set; }

        public void OnStarted(OpusHostContext context)
        {
            StartedWindowOpen = context.Window?.IsOpen == true;
        }

        public void FixedTick(GameTime time)
        {
            FixedTicks.Add(time.Tick.Value);
        }

        public void Render(OpusRenderFrame frame)
        {
            RenderFrames.Add(frame);
        }

        public void OnPaused(OpusHostContext context)
        {
            PausedCount++;
        }

        public void OnResumed(OpusHostContext context)
        {
            ResumedCount++;
        }

        public void OnStopping(OpusHostContext context)
        {
            StoppingCount++;
        }
    }

    private sealed class RecordingLifecycleService : ILifecycleService
    {
        public event Action<LifecycleState, LifecycleState>? StateChanged;

        public event Action? ShuttingDown;

        public LifecycleState State { get; private set; } = LifecycleState.Foreground;

        public void RaiseShuttingDown()
        {
            var previous = State;
            State = LifecycleState.ShuttingDown;
            StateChanged?.Invoke(previous, State);
            ShuttingDown?.Invoke();
        }
    }

    private sealed class RecordingWindowService : IWindowService
    {
        public event Action? Opened;

        public event Action? CloseRequested;

        public event Action<int, int>? Resized;

        public bool IsOpen { get; private set; }

        public (int Width, int Height) Size { get; private set; }

        public string Title { get; set; } = string.Empty;

        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public bool RequestCloseOnNextPoll { get; init; }

        public void Open(WindowOptions options)
        {
            if (IsOpen)
            {
                return;
            }

            OpenCount++;
            IsOpen = true;
            Title = options.Title;
            Size = (options.Width, options.Height);
            Opened?.Invoke();
        }

        public void PollEvents()
        {
            if (RequestCloseOnNextPoll)
            {
                CloseRequested?.Invoke();
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            CloseCount++;
            IsOpen = false;
            CloseRequested?.Invoke();
        }

        public void Dispose()
        {
            Close();
        }

        public void Resize(int width, int height)
        {
            Size = (width, height);
            Resized?.Invoke(width, height);
        }
    }
}
