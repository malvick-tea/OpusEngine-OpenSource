using System;
using System.Collections.Generic;

namespace Opus.Foundation;

/// <summary>
/// Fan-out <see cref="ILog"/> implementation used by hosts that need to write the same
/// event to console, rolling files, and other sinks without coupling call sites to a
/// logging framework.
/// <para>
/// Each sink is isolated: a single sink throwing in <see cref="ILog.Log"/> does not
/// prevent the remaining sinks from receiving the entry. After <see cref="Dispose"/>
/// further <see cref="Log"/> calls are silent no-ops so a top-level crash handler can
/// still log freely against a torn-down composite without itself faulting.
/// </para>
/// </summary>
public sealed class CompositeLog : ILog, IDisposable
{
    private readonly ILog[] _sinks;
    private bool _disposed;

    /// <summary>Creates a composite sink from an explicit sink list. Rejects null elements
    /// loudly so wiring mistakes surface immediately rather than turning into a silently
    /// reduced fan-out.</summary>
    public CompositeLog(IReadOnlyCollection<ILog> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        if (sinks.Count == 0)
        {
            throw new ArgumentException("At least one log sink is required.", nameof(sinks));
        }

        var copy = new ILog[sinks.Count];
        var index = 0;
        foreach (var sink in sinks)
        {
            if (sink is null)
            {
                throw new ArgumentException(
                    "Composite log sink list contains a null entry.",
                    nameof(sinks));
            }

            copy[index++] = sink;
        }

        _sinks = copy;
    }

    /// <summary>Creates a composite sink from a params array.</summary>
    public static CompositeLog Create(params ILog[] sinks) => new(sinks);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel level)
    {
        if (_disposed)
        {
            return false;
        }

        for (var i = 0; i < _sinks.Length; i++)
        {
            if (_sinks[i].IsEnabled(level))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_disposed)
        {
            return;
        }

        for (var i = 0; i < _sinks.Length; i++)
        {
            var sink = _sinks[i];
            if (!sink.IsEnabled(level))
            {
                continue;
            }

            try
            {
                sink.Log(level, message, exception);
            }
            catch
            {
                // Sink-level failures are isolated. A faulty sink must not silence other
                // sinks (especially the console sink the host uses for last-resort error
                // visibility). The faulty sink owns its own diagnostics; rethrowing here
                // would surprise call sites that expect logging to be best-effort.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        for (var i = _sinks.Length - 1; i >= 0; i--)
        {
            if (_sinks[i] is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Mirror Log: a single sink's Dispose throwing must not prevent
                    // remaining sinks from releasing their resources.
                }
            }
        }
    }
}
