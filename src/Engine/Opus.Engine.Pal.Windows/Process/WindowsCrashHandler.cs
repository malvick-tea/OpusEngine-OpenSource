using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Opus.Engine.Pal.Process;

namespace Opus.Engine.Pal.Windows.Process;

/// <summary>
/// Minimal AppDomain-level catcher for unhandled exceptions. Real minidump emission and
/// symbol upload happen later (layout v2 §7 — <c>MinidumpWriter</c>); this M1b version
/// just writes a flat text dump alongside the user log directory and fires the event so
/// the splash / error screen can pick it up next launch.
/// </summary>
public sealed class WindowsCrashHandler : ICrashHandler
{
    private readonly string _crashDir;
    private readonly ConcurrentQueue<(string Category, string Message)> _breadcrumbs = new();
    private bool _installed;

    public WindowsCrashHandler(string crashDir)
    {
        _crashDir = crashDir;
    }

    public bool IsInstalled => _installed;

    public event Action<CrashReport>? CrashCaptured;

    public void Install()
    {
        if (_installed)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
        _installed = true;
    }

    public void AddBreadcrumb(string category, string message)
    {
        _breadcrumbs.Enqueue((category, message));
        while (_breadcrumbs.Count > 64 && _breadcrumbs.TryDequeue(out _))
        {
            // keep last 64 entries.
        }
    }

    private void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception ex)
        {
            return;
        }

        Directory.CreateDirectory(_crashDir);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(_crashDir, $"crash-{stamp}.txt");

        try
        {
            using var w = new StreamWriter(path);
            w.WriteLine($"# Opus crash @ {stamp}");
            w.WriteLine($"## Exception: {ex.GetType().FullName}: {ex.Message}");
            w.WriteLine();
            w.WriteLine("## Stack trace:");
            w.WriteLine(ex.ToString());
            w.WriteLine();
            w.WriteLine("## Breadcrumbs:");
            foreach (var (c, m) in _breadcrumbs)
            {
                w.WriteLine($"  [{c}] {m}");
            }
        }
        catch
        {
            // If we can't even write the crash, swallow — the OS is about to take us down anyway.
        }

        CrashCaptured?.Invoke(new CrashReport(
            ExceptionType: ex.GetType().FullName ?? "<unknown>",
            Message: ex.Message,
            StackTrace: ex.StackTrace ?? string.Empty,
            CapturedAt: DateTimeOffset.UtcNow,
            MinidumpPath: path));
    }
}
