using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Opus.Foundation;

/// <summary>
/// Session-scoped rolling file implementation of <see cref="IRollingLogSink"/>.
/// <para>
/// Writes are lock-protected and synchronous. Each session opens its own log file using
/// the configured prefix plus a UTC timestamp; if a sibling process opens a file with the
/// same stem in the same second, the sink retries with a numeric suffix instead of
/// crashing. After <see cref="Dispose"/> further <see cref="Log"/> calls are silent no-ops
/// so an unrelated exception path can still log freely against a torn-down composite sink.
/// </para>
/// </summary>
public sealed class RollingFileLogSink : IRollingLogSink
{
    private const string LogExtension = ".log";
    private const string HeaderPrefix = "# ";
    private const int MaxCollisionRetries = 64;

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly object _writeLock = new();
    private readonly RollingLogSinkOptions _options;
    private readonly TimeProvider _clock;
    private readonly Queue<RollingLogEntry> _tail;
    private readonly string _sessionStamp;
    private FileStream _stream;
    private StreamWriter _writer;
    private string _currentLogFilePath;
    private int _rollIndex;
    private bool _disposed;

    /// <summary>Opens a rolling log sink and writes the session header immediately.</summary>
    public RollingFileLogSink(RollingLogSinkOptions options)
        : this(options, TimeProvider.System)
    {
    }

    /// <summary>Test-friendly constructor with an explicit clock.</summary>
    public RollingFileLogSink(RollingLogSinkOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        options.Validate();

        _options = options;
        _clock = clock;
        _tail = new Queue<RollingLogEntry>(options.MaxTailEntries);
        _sessionStamp = clock.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(options.DirectoryPath);
        RollingLogRetentionSweeper.Sweep(
            options.DirectoryPath,
            options.FileNamePrefix,
            options.EffectiveRetention,
            clock.GetUtcNow());
        (_stream, _currentLogFilePath) = OpenWithCollisionRetry();
        _writer = WrapStream(_stream);
        WriteHeader();
    }

    /// <inheritdoc />
    public string CurrentLogFilePath
    {
        get
        {
            lock (_writeLock)
            {
                return _currentLogFilePath;
            }
        }
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel level) =>
        level != LogLevel.None && level >= _options.MinimumLevel;

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(message);
        var entry = RollingLogEntry.Create(_clock.GetUtcNow(), level, message, exception);
        LogEntry(entry);
    }

    /// <inheritdoc />
    public void LogEntry(RollingLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!IsEnabled(entry.Level))
        {
            return;
        }

        lock (_writeLock)
        {
            if (_disposed)
            {
                // After Dispose the sink becomes a silent no-op so a host-level catch
                // handler logging into a torn-down composite cannot itself crash.
                return;
            }

            Retain(entry);
            WriteEntry(entry);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RollingLogEntry> SnapshotTail(int maxEntries)
    {
        if (maxEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be at least 1.");
        }

        lock (_writeLock)
        {
            var available = _tail.Count;
            if (available == 0)
            {
                return Array.Empty<RollingLogEntry>();
            }

            var take = Math.Min(maxEntries, available);
            var skip = available - take;
            var result = new RollingLogEntry[take];
            var index = 0;
            foreach (var entry in _tail)
            {
                if (index >= skip)
                {
                    result[index - skip] = entry;
                }

                index++;
            }

            return result;
        }
    }

    /// <inheritdoc />
    public void Flush(bool toDisk)
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _writer.Flush();
            if (toDisk)
            {
                _stream.Flush(flushToDisk: true);
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

        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _writer.Dispose();
            _disposed = true;
        }
    }

    private void Retain(RollingLogEntry entry)
    {
        _tail.Enqueue(entry);
        while (_tail.Count > _options.MaxTailEntries)
        {
            _tail.Dequeue();
        }
    }

    private void WriteEntry(RollingLogEntry entry)
    {
        var line = entry.ToDisplayLine();
        RotateIfNeeded(line);
        WriteLine(line);
        var continuation = entry.ToExceptionContinuationLine();
        if (continuation is not null)
        {
            WriteLine(continuation);
        }
    }

    private void RotateIfNeeded(string nextLine)
    {
        if (_stream.Position + EstimateLineBytes(nextLine) <= _options.MaxFileBytes)
        {
            return;
        }

        _writer.Dispose();
        _rollIndex++;
        (_stream, _currentLogFilePath) = OpenWithCollisionRetry();
        _writer = WrapStream(_stream);
        WriteHeader();
    }

    private void WriteHeader()
    {
        WriteLine(HeaderPrefix + BuildInfo.Current.ToBannerLine());
        WriteLine(HeaderPrefix + string.Create(
            CultureInfo.InvariantCulture,
            $"sessionUtc={_clock.GetUtcNow():O} minLevel={_options.MinimumLevel} maxFileBytes={_options.MaxFileBytes}"));
    }

    private void WriteLine(string line)
    {
        _writer.WriteLine(line);
    }

    private (FileStream Stream, string Path) OpenWithCollisionRetry()
    {
        for (var attempt = 0; attempt <= MaxCollisionRetries; attempt++)
        {
            var path = BuildPath(attempt);
            try
            {
                var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read);
                return (stream, path);
            }
            catch (IOException) when (attempt < MaxCollisionRetries && File.Exists(path))
            {
                // Another process/sink opened a file with the same stem in the same
                // session second. Append a numeric collision suffix and retry.
                continue;
            }
        }

        throw new IOException(
            $"Could not allocate a rolling log file under '{_options.DirectoryPath}' after {MaxCollisionRetries + 1} attempts.");
    }

    private string BuildPath(int collisionAttempt)
    {
        var rollSuffix = _rollIndex == 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"-{_rollIndex:D3}");
        var collisionSuffix = collisionAttempt == 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"-{collisionAttempt:D2}");
        return Path.Combine(
            _options.DirectoryPath,
            _options.FileNamePrefix + "-" + _sessionStamp + rollSuffix + collisionSuffix + LogExtension);
    }

    private static StreamWriter WrapStream(FileStream stream) => new(stream, Utf8NoBom)
    {
        AutoFlush = true,
    };

    private static long EstimateLineBytes(string line) =>
        Utf8NoBom.GetByteCount(line) + Utf8NoBom.GetByteCount(Environment.NewLine);
}
