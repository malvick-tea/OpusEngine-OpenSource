using Opus.Engine.Pal.Application;
using Opus.Foundation;

namespace Opus.Engine.Runtime;

/// <summary>
/// Minimal Opus runtime host: starts optional PAL services, advances deterministic
/// fixed ticks, emits one render callback per variable frame, and shuts down once.
/// </summary>
public sealed class OpusHost : IDisposable
{
    private const double FixedTickEpsilonSeconds = 0.0000001;

    private readonly IOpusApplication _application;
    private readonly IWindowService? _window;
    private readonly ILifecycleService? _lifecycle;
    private readonly OpusHostContext _context;
    private readonly double _fixedTickSeconds;
    private double _accumulatorSeconds;
    private bool _shutdownRequested;
    private bool _stopCallbackSent;
    private bool _disposed;
    private ulong _frameIndex;

    public OpusHost(
        IOpusApplication application,
        OpusHostOptions? options = null,
        IWindowService? window = null,
        ILifecycleService? lifecycle = null)
    {
        _application = Ensure.NotNull(application);
        Options = options ?? new OpusHostOptions();
        Options.Validate(nameof(options));

        _window = window;
        _lifecycle = lifecycle;
        _context = new OpusHostContext(Options, _window, _lifecycle);
        Time = GameTime.AtRate(Options.TickRateHz);
        _fixedTickSeconds = Time.TickIntervalSeconds;

        if (_window is not null)
        {
            _window.CloseRequested += RequestShutdown;
        }

        if (_lifecycle is not null)
        {
            _lifecycle.ShuttingDown += RequestShutdown;
        }
    }

    public OpusHostOptions Options { get; }

    public OpusHostState State { get; private set; } = OpusHostState.Created;

    public GameTime Time { get; private set; }

    public ulong FrameIndex => _frameIndex;

    public long FixedTicksExecuted { get; private set; }

    public TimeSpan DroppedFixedTime { get; private set; }

    public bool IsShutdownRequested => _shutdownRequested;

    public void Start()
    {
        if (State is OpusHostState.Running or OpusHostState.Paused)
        {
            return;
        }

        if (State is OpusHostState.Stopped or OpusHostState.ShuttingDown)
        {
            throw new InvalidOperationException("A stopped OpusHost cannot be restarted.");
        }

        if (_window is not null && Options.Window is { } windowOptions)
        {
            _window.Open(windowOptions);
        }

        State = OpusHostState.Running;
        _application.OnStarted(_context);
    }

    public bool Step(TimeSpan frameDelta)
    {
        if (frameDelta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(frameDelta), frameDelta, "Frame delta cannot be negative.");
        }

        EnsureStarted();

        if (State == OpusHostState.Stopped)
        {
            return false;
        }

        _window?.PollEvents();
        if (_shutdownRequested)
        {
            Stop();
            return false;
        }

        var fixedTicksThisFrame = 0;
        if (State == OpusHostState.Running)
        {
            fixedTicksThisFrame = DrainFixedTicks(frameDelta);
        }

        if (State == OpusHostState.Running || (State == OpusHostState.Paused && Options.RenderWhilePaused))
        {
            Render(frameDelta, fixedTicksThisFrame);
        }

        return State != OpusHostState.Stopped;
    }

    public void Pause()
    {
        EnsureStarted();
        if (State != OpusHostState.Running)
        {
            return;
        }

        State = OpusHostState.Paused;
        _application.OnPaused(_context);
    }

    public void Resume()
    {
        EnsureStarted();
        if (State != OpusHostState.Paused)
        {
            return;
        }

        State = OpusHostState.Running;
        _application.OnResumed(_context);
    }

    public void RequestShutdown()
    {
        if (State == OpusHostState.Stopped)
        {
            return;
        }

        _shutdownRequested = true;
        State = OpusHostState.ShuttingDown;
    }

    public void Stop()
    {
        if (State == OpusHostState.Stopped)
        {
            return;
        }

        State = OpusHostState.ShuttingDown;
        _shutdownRequested = true;
        if (!_stopCallbackSent)
        {
            _application.OnStopping(_context);
            _stopCallbackSent = true;
        }

        if (_window is { IsOpen: true })
        {
            _window.Close();
        }

        State = OpusHostState.Stopped;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        if (_window is not null)
        {
            _window.CloseRequested -= RequestShutdown;
        }

        if (_lifecycle is not null)
        {
            _lifecycle.ShuttingDown -= RequestShutdown;
        }

        _disposed = true;
    }

    private int DrainFixedTicks(TimeSpan frameDelta)
    {
        _accumulatorSeconds += frameDelta.TotalSeconds;
        var ticksThisFrame = 0;
        while (_accumulatorSeconds + FixedTickEpsilonSeconds >= _fixedTickSeconds
            && ticksThisFrame < Options.MaxFixedTicksPerFrame)
        {
            _application.FixedTick(Time);
            Time = Time.Advance();
            _accumulatorSeconds -= _fixedTickSeconds;
            NormalizeAccumulator();
            ticksThisFrame++;
            FixedTicksExecuted++;
        }

        if (ticksThisFrame == Options.MaxFixedTicksPerFrame
            && _accumulatorSeconds + FixedTickEpsilonSeconds >= _fixedTickSeconds)
        {
            var wholeTicksToDrop = Math.Floor((_accumulatorSeconds + FixedTickEpsilonSeconds) / _fixedTickSeconds);
            var droppedSeconds = wholeTicksToDrop * _fixedTickSeconds;
            _accumulatorSeconds -= droppedSeconds;
            NormalizeAccumulator();
            DroppedFixedTime += TimeSpan.FromSeconds(droppedSeconds);
        }

        return ticksThisFrame;
    }

    private void Render(TimeSpan frameDelta, int fixedTicksThisFrame)
    {
        var alpha = Math.Clamp(_accumulatorSeconds / _fixedTickSeconds, 0.0, 1.0);
        _frameIndex++;
        _application.Render(new OpusRenderFrame(Time, frameDelta, alpha, fixedTicksThisFrame, _frameIndex));
    }

    private void EnsureStarted()
    {
        if (State == OpusHostState.Created)
        {
            throw new InvalidOperationException("Start the OpusHost before driving frames.");
        }
    }

    private void NormalizeAccumulator()
    {
        if (Math.Abs(_accumulatorSeconds) <= FixedTickEpsilonSeconds)
        {
            _accumulatorSeconds = 0;
        }
    }
}
