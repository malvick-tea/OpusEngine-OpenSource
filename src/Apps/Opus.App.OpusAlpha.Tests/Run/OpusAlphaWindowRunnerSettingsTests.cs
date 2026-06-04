using System;
using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.App.OpusAlpha.Run;
using Opus.Engine.AlphaHarness.Scenes;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class OpusAlphaWindowRunnerSettingsTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "opus-runner-settings-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ApplyTesterSettings_overlays_persisted_window_knobs_and_they_reach_the_options()
    {
        var path = Path.Combine(_root, "tester.json");
        var saved = new TesterSettings(
            AlphaSceneScale.Large, DiagnosticOverlayLevel.Off, EnableFrameBudget: true, EnableAsyncLogging: true);
        TesterSettingsStore.TrySave(path, saved, NullLog.Instance).Should().BeTrue();
        var args = OpusAlphaArgs.WindowDefaults() with { SettingsPath = path };

        var effective = OpusAlphaWindowRunner.ApplyTesterSettings(args, NullLog.Instance);

        effective.SceneScale.Should().Be(AlphaSceneScale.Large);
        effective.OverlayLevel.Should().Be(DiagnosticOverlayLevel.Off);
        effective.EnableFrameBudget.Should().BeTrue();
        effective.EnableAsyncLogging.Should().BeTrue();

        var options = OpusAlphaWindowRunner.BuildOptions(effective, consumerIntegration: null);
        options.SceneScale.Should().Be(AlphaSceneScale.Large);
        options.EffectiveFrameBudget.Enabled.Should().BeTrue();
        options.EffectiveDiagnosticOverlayOptions.Level.Should().Be(DiagnosticOverlayLevel.Off);
    }

    [Fact]
    public void ApplyTesterSettings_seeds_a_default_file_when_the_path_is_new()
    {
        var path = Path.Combine(_root, "fresh.json");
        var args = OpusAlphaArgs.WindowDefaults() with { SettingsPath = path };

        var effective = OpusAlphaWindowRunner.ApplyTesterSettings(args, NullLog.Instance);

        File.Exists(path).Should().BeTrue();
        effective.SceneScale.Should().Be(TesterSettings.Default.SceneScale);
    }

    [Fact]
    public void ApplyTesterSettings_is_a_no_op_without_a_settings_path()
    {
        var args = OpusAlphaArgs.WindowDefaults();

        var effective = OpusAlphaWindowRunner.ApplyTesterSettings(args, NullLog.Instance);

        effective.Should().Be(args);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
