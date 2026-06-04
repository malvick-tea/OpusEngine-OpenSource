using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.App.OpusAlpha.Run;
using Opus.Engine.AlphaHarness.Scenes;
using Opus.Engine.Diagnostics.Overlay;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class OpusAlphaWindowRunnerOptionsTests
{
    [Fact]
    public void BuildOptions_applies_the_requested_overlay_level()
    {
        var args = OpusAlphaArgs.WindowDefaults() with { OverlayLevel = DiagnosticOverlayLevel.Off };

        var options = OpusAlphaWindowRunner.BuildOptions(args, consumerIntegration: null);

        options.EffectiveDiagnosticOverlayOptions.Level.Should().Be(DiagnosticOverlayLevel.Off);
        options.EffectiveDiagnosticOverlayOptions.ShouldDraw.Should().BeFalse();
    }

    [Fact]
    public void BuildOptions_leaves_the_overlay_at_the_full_default()
    {
        var args = OpusAlphaArgs.WindowDefaults();

        var options = OpusAlphaWindowRunner.BuildOptions(args, consumerIntegration: null);

        options.EffectiveDiagnosticOverlayOptions.Level.Should().Be(DiagnosticOverlayLevel.Full);
        options.EffectiveDiagnosticOverlayOptions.ShouldDraw.Should().BeTrue();
    }

    [Fact]
    public void BuildReportWriterOptions_applies_the_active_retention_budget()
    {
        var options = OpusAlphaWindowRunner.BuildReportWriterOptions("reports-root");

        options.DirectoryPath.Should().Be("reports-root");
        options.EffectiveRetention.IsActive.Should().BeTrue();
        options.EffectiveRetention.Should().Be(OpusAlphaRetention.Artifacts);
    }

    [Fact]
    public void BuildOptions_enables_the_frame_budget_watchdog_when_requested()
    {
        var args = OpusAlphaArgs.WindowDefaults() with { EnableFrameBudget = true };

        var options = OpusAlphaWindowRunner.BuildOptions(args, consumerIntegration: null);

        options.EffectiveFrameBudget.Enabled.Should().BeTrue();
    }

    [Fact]
    public void BuildOptions_leaves_the_frame_budget_watchdog_disabled_by_default()
    {
        var args = OpusAlphaArgs.WindowDefaults();

        var options = OpusAlphaWindowRunner.BuildOptions(args, consumerIntegration: null);

        options.EffectiveFrameBudget.Enabled.Should().BeFalse();
    }

    [Fact]
    public void BuildOptions_maps_scene_scale_and_asset_path()
    {
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            SceneScale = AlphaSceneScale.Large,
            AssetPath = "tank.glb",
        };

        var options = OpusAlphaWindowRunner.BuildOptions(args, consumerIntegration: null);

        options.SceneScale.Should().Be(AlphaSceneScale.Large);
        options.AssetPath.Should().Be("tank.glb");
    }
}
