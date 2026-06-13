using System;
using System.Globalization;
using System.IO;

namespace Opus.Foundation;

/// <summary>
/// Minimal <see cref="ILog"/> sink that emits one timestamped line per call to a
/// standard-output writer (and standard-error for <see cref="LogLevel.Warning"/> and
/// above). Suitable as the default-on sink for the Opus 0.1 alpha sample host, smoke
/// tests, and developer-machine debugging without dragging Serilog or
/// <c>Microsoft.Extensions.Logging</c> into the Foundation layer.
///
/// Production hosts that need rolling files, structured JSON, or remote sinks compose a
/// full logger of their choice and adapt it to <see cref="ILog"/>. ConsoleLog is the
/// tiny zero-config fallback so a host can start logging without registering anything.
/// </summary>
public sealed class ConsoleLog : ILog
{
    private static readonly string[] LevelTags =
    {
        "TRC",
        "DBG",
        "INF",
        "WRN",
        "ERR",
        "CRT",
        "OFF",
    };

    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly TimeProvider _clock;
    private readonly object _writeLock = new();

    /// <summary>Convenience constructor: writes to <see cref="Console.Out"/> +
    /// <see cref="Console.Error"/> using the system clock, filtering at
    /// <paramref name="minimumLevel"/> (default <see cref="LogLevel.Information"/>).</summary>
    public ConsoleLog(LogLevel minimumLevel = LogLevel.Information)
        : this(minimumLevel, Console.Out, Console.Error, TimeProvider.System)
    {
    }

    /// <summary>Test-friendly constructor accepting explicit writers and clock seam.</summary>
    public ConsoleLog(LogLevel minimumLevel, TextWriter standardOut, TextWriter standardError, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(standardOut);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(clock);

        MinimumLevel = minimumLevel;
        _out = standardOut;
        _err = standardError;
        _clock = clock;
    }

    public LogLevel MinimumLevel { get; }

    public bool IsEnabled(LogLevel level) =>
        level != LogLevel.None && level >= MinimumLevel;

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(message);

        var sink = level >= LogLevel.Warning ? _err : _out;
        var line = FormatLine(level, message, exception);
        lock (_writeLock)
        {
            sink.WriteLine(line);
        }
    }

    private string FormatLine(LogLevel level, string message, Exception? exception)
    {
        var localNow = _clock.GetLocalNow();
        var tag = LevelTag(level);

        if (exception is null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"[{localNow:HH:mm:ss.fff} {tag}] {message}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{localNow:HH:mm:ss.fff} {tag}] {message}{Environment.NewLine}  -> {exception.GetType().Name}: {exception.Message}");
    }

    private static string LevelTag(LogLevel level)
    {
        var index = (int)level;
        return index >= 0 && index < LevelTags.Length ? LevelTags[index] : LevelTags[2];
    }
}
