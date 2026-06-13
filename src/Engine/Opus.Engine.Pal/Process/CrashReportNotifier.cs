using System;

namespace Opus.Engine.Pal.Process;

/// <summary>Pure-logic glue between <see cref="ICrashHandler"/> and the platform
/// <see cref="ICrashReportPresenter"/> / <see cref="ICrashRestartLauncher"/>. Subscribes
/// to <see cref="ICrashHandler.CrashCaptured"/> via <see cref="Attach"/>; when a crash
/// fires, asks the presenter what the user wants, and (if Restart) calls the launcher.
/// Stays platform-free so a unit test can supply mock presenters/launchers.</summary>
/// <remarks>
/// <see cref="Handle"/> never throws — presenter failures are mapped to
/// <see cref="CrashReportUserChoice.Quit"/> via the contract on
/// <see cref="ICrashReportPresenter.Show"/>; launcher failures swallow + log to the
/// last-resort sink in the impl. The notifier itself only orchestrates.
/// </remarks>
public sealed class CrashReportNotifier
{
    private readonly ICrashReportPresenter _presenter;
    private readonly ICrashRestartLauncher _launcher;

    public CrashReportNotifier(ICrashReportPresenter presenter, ICrashRestartLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(launcher);
        _presenter = presenter;
        _launcher = launcher;
    }

    /// <summary>The user choice that came back from the most recent
    /// <see cref="Handle"/> call. Null until the first crash. Visible primarily for
    /// tests; production code reacts to the launcher-restart side effect.</summary>
    public CrashReportUserChoice? LastChoice { get; private set; }

    /// <summary>Wires this notifier to <paramref name="crashHandler"/>'s
    /// <see cref="ICrashHandler.CrashCaptured"/> event. Call once during boot, after
    /// <see cref="ICrashHandler.Install"/>.</summary>
    public void Attach(ICrashHandler crashHandler)
    {
        ArgumentNullException.ThrowIfNull(crashHandler);
        crashHandler.CrashCaptured += Handle;
    }

    /// <summary>Shows the report via the presenter and, if the user chose
    /// <see cref="CrashReportUserChoice.Restart"/>, asks the launcher to relaunch.
    /// Public so tests can drive the logic directly without firing the
    /// <see cref="ICrashHandler.CrashCaptured"/> event.</summary>
    public void Handle(CrashReport report)
    {
        var choice = _presenter.Show(report);
        LastChoice = choice;
        if (choice == CrashReportUserChoice.Restart)
        {
            _launcher.RelaunchProcess();
        }
    }
}
