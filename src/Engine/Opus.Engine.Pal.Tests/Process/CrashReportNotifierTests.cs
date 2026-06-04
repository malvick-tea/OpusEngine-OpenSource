using System;
using FluentAssertions;
using Opus.Engine.Pal.Process;
using Xunit;

namespace Opus.Engine.Pal.Tests.Process;

/// <summary>Pure-logic tests for <see cref="CrashReportNotifier"/> — no Win32, no
/// processes. Verifies that the user's choice from the presenter routes correctly to
/// the launcher's <see cref="ICrashRestartLauncher.RelaunchProcess"/> hook.</summary>
public sealed class CrashReportNotifierTests
{
    private static readonly CrashReport SampleReport = new(
        ExceptionType: "System.InvalidOperationException",
        Message: "boom",
        StackTrace: "at Foo.Bar()",
        CapturedAt: DateTimeOffset.UnixEpoch,
        MinidumpPath: "user://crashes/crash-20260519.txt");

    [Fact]
    public void Handle_calls_RelaunchProcess_when_user_picks_Restart()
    {
        var presenter = new ScriptedPresenter(CrashReportUserChoice.Restart);
        var launcher = new RecordingLauncher();
        var notifier = new CrashReportNotifier(presenter, launcher);

        notifier.Handle(SampleReport);

        launcher.RelaunchCalls.Should().Be(1);
        notifier.LastChoice.Should().Be(CrashReportUserChoice.Restart);
    }

    [Fact]
    public void Handle_does_not_call_RelaunchProcess_when_user_picks_Quit()
    {
        var presenter = new ScriptedPresenter(CrashReportUserChoice.Quit);
        var launcher = new RecordingLauncher();
        var notifier = new CrashReportNotifier(presenter, launcher);

        notifier.Handle(SampleReport);

        launcher.RelaunchCalls.Should().Be(0);
        notifier.LastChoice.Should().Be(CrashReportUserChoice.Quit);
    }

    [Fact]
    public void Handle_invokes_the_presenter_exactly_once_per_call()
    {
        var presenter = new ScriptedPresenter(CrashReportUserChoice.Quit);
        var notifier = new CrashReportNotifier(presenter, new RecordingLauncher());

        notifier.Handle(SampleReport);
        notifier.Handle(SampleReport);
        notifier.Handle(SampleReport);

        presenter.ShowCalls.Should().Be(3);
    }

    [Fact]
    public void Attach_subscribes_to_CrashCaptured_so_event_drives_Handle()
    {
        var presenter = new ScriptedPresenter(CrashReportUserChoice.Restart);
        var launcher = new RecordingLauncher();
        var notifier = new CrashReportNotifier(presenter, launcher);
        var crashHandler = new FakeCrashHandler();

        notifier.Attach(crashHandler);
        crashHandler.SimulateCrash(SampleReport);

        launcher.RelaunchCalls.Should().Be(1);
        notifier.LastChoice.Should().Be(CrashReportUserChoice.Restart);
    }

    [Fact]
    public void Constructor_rejects_null_presenter_or_launcher()
    {
        Action nullPresenter = () => _ = new CrashReportNotifier(null!, new RecordingLauncher());
        Action nullLauncher = () => _ = new CrashReportNotifier(new ScriptedPresenter(CrashReportUserChoice.Quit), null!);

        nullPresenter.Should().Throw<ArgumentNullException>();
        nullLauncher.Should().Throw<ArgumentNullException>();
    }

    private sealed class ScriptedPresenter : ICrashReportPresenter
    {
        private readonly CrashReportUserChoice _choice;

        public ScriptedPresenter(CrashReportUserChoice choice)
        {
            _choice = choice;
        }

        public int ShowCalls { get; private set; }

        public CrashReportUserChoice Show(CrashReport report)
        {
            ShowCalls++;
            return _choice;
        }
    }

    private sealed class RecordingLauncher : ICrashRestartLauncher
    {
        public int RelaunchCalls { get; private set; }

        public void RelaunchProcess() => RelaunchCalls++;
    }

    private sealed class FakeCrashHandler : ICrashHandler
    {
        public bool IsInstalled => true;

        public event Action<CrashReport>? CrashCaptured;

        public void Install()
        {
        }

        public void AddBreadcrumb(string category, string message)
        {
        }

        public void SimulateCrash(CrashReport report) => CrashCaptured?.Invoke(report);
    }
}
