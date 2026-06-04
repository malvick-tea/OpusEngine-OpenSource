using System;
using Opus.Engine.Pal.Process;
using SystemProcess = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Opus.Engine.Pal.Windows.Process;

/// <summary>Spawns a fresh copy of the running executable via
/// <c>System.Diagnostics.Process.Start</c>. Used by
/// <see cref="CrashReportNotifier"/> when the user picks
/// <see cref="CrashReportUserChoice.Restart"/> on the crash dialog.</summary>
/// <remarks>
/// Does NOT terminate the current process — returning lets the unhandled-exception path
/// run to completion; the CLR then takes the process down. The newly-spawned process
/// inherits no working state, exactly like a fresh launch from the shell, so progress
/// continuity comes from <see cref="Persistence.ISaveStore"/>'s last write. Errors are
/// written to <see cref="Console.Error"/> as a last-resort sink — the CLR's normal
/// logging providers are already torn down by the time the crash path runs.
/// </remarks>
public sealed class WindowsRestartLauncher : ICrashRestartLauncher
{
    public void RelaunchProcess()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            Console.Error.WriteLine("[Opus] Cannot relaunch — Environment.ProcessPath is empty.");
            return;
        }

        try
        {
            SystemProcess.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Opus] Relaunch failed for {exePath}: {ex.Message}");
        }
    }
}
