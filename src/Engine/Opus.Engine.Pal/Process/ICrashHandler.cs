using System;

namespace Opus.Engine.Pal.Process;

/// <summary>
/// Top-level unhandled-exception / SEH / signal catcher. Engine.Pal.{platform} installs
/// the actual hook; the contract here lets host code register breadcrumbs and decide
/// whether to upload the minidump on the next launch.
/// </summary>
public interface ICrashHandler
{
    /// <summary>True after Install() has been called.</summary>
    bool IsInstalled { get; }

    event Action<CrashReport>? CrashCaptured;

    void Install();

    /// <summary>Adds a single breadcrumb to the current crash buffer (last N retained).</summary>
    void AddBreadcrumb(string category, string message);
}

/// <summary>Compact crash record. Real serialization happens in Engine.Diagnostics.</summary>
public readonly record struct CrashReport(
    string ExceptionType,
    string Message,
    string StackTrace,
    System.DateTimeOffset CapturedAt,
    string MinidumpPath);
