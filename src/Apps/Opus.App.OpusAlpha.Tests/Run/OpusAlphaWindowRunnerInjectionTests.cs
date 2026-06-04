using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.App.OpusAlpha.Run;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

/// <summary>
/// End-to-end coverage of the alpha host's failure-diagnostics path driven through the real
/// <see cref="OpusAlphaWindowRunner"/>. Injection is raised before the host build, so these tests
/// run headlessly (no GPU, no window) while still exercising the runtime classify / capture /
/// write pipeline a genuine crash would take.
/// </summary>
public sealed class OpusAlphaWindowRunnerInjectionTests : IDisposable
{
    private const int ExitCrash = 2;

    private readonly string _root;

    public OpusAlphaWindowRunnerInjectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "opus-alpha-inject-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(AlphaFaultKind.Startup, "startupfailure")]
    [InlineData(AlphaFaultKind.Content, "contentfailure")]
    [InlineData(AlphaFaultKind.DeviceLost, "devicelost")]
    public void Injected_failure_writes_report_bundle_of_expected_kind(AlphaFaultKind kind, string stemKind)
    {
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            DiagnosticsDirectory = _root,
            InjectFailure = kind,
        };
        var log = new CapturingLog();

        var exit = OpusAlphaWindowRunner.Run(args, log);

        exit.Should().Be(ExitCrash);
        var reports = Directory.GetFiles(_root, $"opus-{stemKind}-*.txt", SearchOption.AllDirectories);
        reports.Should().ContainSingle("the injected {0} failure must leave exactly one report bundle", kind);
    }

    [Fact]
    public void Injected_failure_with_unwritable_diagnostics_root_is_handled_without_crashing()
    {
        // Point the diagnostics root at an existing FILE so every directory creation under it
        // fails. The writer must surface a structured OPDX-REP issue and the host must still exit
        // through its normal failure path, not propagate an unhandled filesystem exception.
        var filePath = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(filePath, "x");
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            DiagnosticsDirectory = filePath,
            InjectFailure = AlphaFaultKind.Content,
        };
        var log = new CapturingLog();

        var exit = OpusAlphaWindowRunner.Run(args, log);

        exit.Should().Be(ExitCrash);
        log.Messages.Should().ContainMatch("*OPDX-REP*");
    }

    private sealed class CapturingLog : ILog
    {
        public List<string> Messages { get; } = new();

        public bool IsEnabled(LogLevel level) => true;

        public void Log(LogLevel level, string message, Exception? exception = null) =>
            Messages.Add(message);
    }
}
