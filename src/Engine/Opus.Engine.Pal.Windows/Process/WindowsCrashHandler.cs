using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using Opus.Engine.Pal.Process;

namespace Opus.Engine.Pal.Windows.Process;

/// <summary>
/// AppDomain-level crash capture with bounded breadcrumbs and explicit unsubscription.
/// </summary>
public sealed class WindowsCrashHandler : ICrashHandler, IDisposable
{
    private const int MaximumBreadcrumbs = 64;
    private const int MaximumCategoryLength = 128;
    private const int MaximumMessageLength = 2048;

    private readonly object _installationSync = new();
    private readonly string _crashDirectory;
    private readonly ConcurrentQueue<(string Category, string Message)> _breadcrumbs = new();
    private int _breadcrumbCount;
    private int _installed;
    private volatile bool _disposed;

    public WindowsCrashHandler(string crashDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(crashDirectory);
        _crashDirectory = Path.GetFullPath(crashDirectory);
    }

    public bool IsInstalled => Volatile.Read(ref _installed) != 0;

    public event Action<CrashReport>? CrashCaptured;

    public void Install()
    {
        lock (_installationSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_installed != 0)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            Volatile.Write(ref _installed, 1);
        }
    }

    public void AddBreadcrumb(string category, string message)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _breadcrumbs.Enqueue((
            Truncate(category, MaximumCategoryLength),
            Truncate(message, MaximumMessageLength)));
        Interlocked.Increment(ref _breadcrumbCount);
        while (Volatile.Read(ref _breadcrumbCount) > MaximumBreadcrumbs
               && _breadcrumbs.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _breadcrumbCount);
        }
    }

    private void OnUnhandled(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is not Exception exception)
        {
            return;
        }

        var capturedAt = DateTimeOffset.UtcNow;
        var stamp = capturedAt.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        var path = Path.Combine(_crashDirectory, $"crash-{stamp}-{Guid.NewGuid():N}.txt");
        try
        {
            Directory.CreateDirectory(_crashDirectory);
            using var writer = new StreamWriter(new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read));
            writer.WriteLine($"# Opus crash @ {stamp}");
            writer.WriteLine($"## Exception: {exception.GetType().FullName}: {exception.Message}");
            writer.WriteLine();
            writer.WriteLine("## Stack trace:");
            writer.WriteLine(exception);
            writer.WriteLine();
            writer.WriteLine("## Breadcrumbs:");
            foreach (var (category, message) in _breadcrumbs)
            {
                writer.WriteLine($"  [{category}] {message}");
            }
        }
        catch
        {
            // Crash capture must not replace the original unhandled exception.
        }

        var report = new CrashReport(
            ExceptionType: exception.GetType().FullName ?? "<unknown>",
            Message: exception.Message,
            StackTrace: exception.StackTrace ?? string.Empty,
            CapturedAt: capturedAt,
            MinidumpPath: path);
        NotifySafely(report);
    }

    private void NotifySafely(CrashReport report)
    {
        var handlers = CrashCaptured;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<CrashReport> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(report);
            }
            catch
            {
                // Observer failures must not interfere with process-level crash handling.
            }
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    public void Dispose()
    {
        lock (_installationSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_installed != 0)
            {
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
                Volatile.Write(ref _installed, 0);
            }
        }
    }
}
