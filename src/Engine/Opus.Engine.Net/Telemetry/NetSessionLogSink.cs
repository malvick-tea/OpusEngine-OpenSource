using System;
using Opus.Engine.Net.Session;
using Opus.Foundation;

namespace Opus.Engine.Net.Telemetry;

/// <summary>
/// Adapter that routes <see cref="NetSessionEvent"/> notifications into a Foundation
/// <see cref="ILog"/> sink. The host wires the sink into <c>NetSession.Tick</c> through
/// <see cref="Observe"/>, so every session lifecycle transition, peer join/leave, and
/// reconnect attempt produces a stable, code-prefixed log line — exactly the packet/log
/// diagnostics hook the roadmap requires for M8.
/// </summary>
public sealed class NetSessionLogSink
{
    private readonly ILog _log;

    public NetSessionLogSink(ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    /// <summary>Observes a single session event. Routes to the appropriate log level
    /// based on the event kind so a tester filter that captures Warning+ never misses a
    /// reconnect or fault event but stays quiet during the steady-state Connected path.</summary>
    public void Observe(NetSessionEvent sessionEvent)
    {
        ArgumentNullException.ThrowIfNull(sessionEvent);
        var line = NetSessionTelemetryFormatter.FormatEventLine(sessionEvent);
        var level = LevelFor(sessionEvent.Kind);
        if (!_log.IsEnabled(level))
        {
            return;
        }

        _log.Log(level, line);
    }

    private static LogLevel LevelFor(NetSessionEventKind kind) => kind switch
    {
        NetSessionEventKind.PayloadReceived => LogLevel.Trace,
        NetSessionEventKind.PeerConnected => LogLevel.Information,
        NetSessionEventKind.PeerDisconnected => LogLevel.Information,
        NetSessionEventKind.Started => LogLevel.Information,
        NetSessionEventKind.Stopped => LogLevel.Information,
        NetSessionEventKind.ReconnectScheduled => LogLevel.Warning,
        NetSessionEventKind.ReconnectAttempted => LogLevel.Warning,
        NetSessionEventKind.ReconnectExhausted => LogLevel.Error,
        NetSessionEventKind.TransportFault => LogLevel.Error,
        _ => LogLevel.Debug,
    };
}
