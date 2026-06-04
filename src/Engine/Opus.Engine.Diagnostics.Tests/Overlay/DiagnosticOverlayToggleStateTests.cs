using FluentAssertions;
using Opus.Engine.Diagnostics.Overlay;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Overlay;

public sealed class DiagnosticOverlayToggleStateTests
{
    [Fact]
    public void Initial_enabled_reflects_constructor_argument()
    {
        new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.F10, initiallyEnabled: true)
            .IsEnabled.Should().BeTrue();
        new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.F10, initiallyEnabled: false)
            .IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsToggleConfigured_is_true_only_when_a_key_is_set()
    {
        new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.F10, initiallyEnabled: true)
            .IsToggleConfigured.Should().BeTrue();
        new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.None, initiallyEnabled: true)
            .IsToggleConfigured.Should().BeFalse();
    }

    [Fact]
    public void HandleKeyDown_on_matching_key_flips_visibility_and_reports_change()
    {
        var state = new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.F10, initiallyEnabled: true);

        var changed = state.HandleKeyDown(DiagnosticOverlayToggleKey.F10);

        changed.Should().BeTrue();
        state.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void HandleKeyDown_ignores_auto_repeat_until_key_is_released()
    {
        var state = new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.F10, initiallyEnabled: true);

        state.HandleKeyDown(DiagnosticOverlayToggleKey.F10).Should().BeTrue();
        state.HandleKeyDown(DiagnosticOverlayToggleKey.F10).Should().BeFalse();
        state.HandleKeyDown(DiagnosticOverlayToggleKey.F10).Should().BeFalse();

        state.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void HandleKeyUp_re_arms_the_toggle_for_a_fresh_press()
    {
        var state = new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.F10, initiallyEnabled: true);

        state.HandleKeyDown(DiagnosticOverlayToggleKey.F10);
        state.HandleKeyUp(DiagnosticOverlayToggleKey.F10);
        state.HandleKeyDown(DiagnosticOverlayToggleKey.F10);

        state.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_on_non_matching_key_does_nothing()
    {
        var state = new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.F10, initiallyEnabled: true);

        var changed = state.HandleKeyDown(DiagnosticOverlayToggleKey.None);

        changed.Should().BeFalse();
        state.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Unconfigured_toggle_never_changes_visibility()
    {
        var state = new DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey.None, initiallyEnabled: true);

        state.HandleKeyDown(DiagnosticOverlayToggleKey.F10).Should().BeFalse();
        state.HandleKeyDown(DiagnosticOverlayToggleKey.None).Should().BeFalse();

        state.IsEnabled.Should().BeTrue();
    }
}
