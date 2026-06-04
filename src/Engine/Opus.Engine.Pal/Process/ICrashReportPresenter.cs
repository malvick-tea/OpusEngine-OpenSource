namespace Opus.Engine.Pal.Process;

/// <summary>How the user wants the host to proceed after a crash report has been shown.
/// Returned by <see cref="ICrashReportPresenter.Show"/>; consumed by
/// <see cref="CrashReportNotifier"/> to decide whether to invoke
/// <see cref="ICrashRestartLauncher"/>.</summary>
public enum CrashReportUserChoice
{
    /// <summary>Player asked to relaunch the game (preserves session continuity).</summary>
    Restart,

    /// <summary>Player asked to terminate quietly without relaunching.</summary>
    Quit,
}

/// <summary>Surface for showing a <see cref="CrashReport"/> to the user and capturing
/// their response. Concrete impls live in <c>Engine.Pal.{platform}</c> — Windows uses a
/// Win32 MessageBox, mobile platforms route through a native alert dialog. The contract
/// is synchronous because crash flows execute on the unhandled-exception path where
/// async continuations cannot be relied on.</summary>
public interface ICrashReportPresenter
{
    /// <summary>Blocks until the user dismisses the report. Implementations must not
    /// throw — any internal failure should fall back to <see cref="CrashReportUserChoice.Quit"/>
    /// so the host still terminates cleanly.</summary>
    CrashReportUserChoice Show(CrashReport report);
}
