using System;
using FluentAssertions;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests;

public sealed class D3D12OpusApplicationOptionsTests
{
    [Fact]
    public void Default_options_match_documented_alpha_host_baseline()
    {
        var options = D3D12OpusApplicationOptions.Default;

        options.WindowTitle.Should().Be(EngineIdentity.Current.DisplayName + " — Alpha Host");
        options.WindowWidth.Should().Be(D3D12OpusApplicationOptions.DefaultWindowWidth);
        options.WindowHeight.Should().Be(D3D12OpusApplicationOptions.DefaultWindowHeight);
        options.EnableDebugLayer.Should().BeFalse();
        options.AssetPath.Should().BeNull();
        options.MetricsWindow.Should().Be(D3D12OpusApplicationOptions.DefaultMetricsWindow);
        options.EffectiveDiagnosticOverlayOptions.Should().Be(DiagnosticOverlayOptions.Default);
        options.EffectiveDiagnosticsDirectory.Should().NotBeNullOrWhiteSpace();
        options.ConsumerIntegration.Should().BeNull();
    }

    [Fact]
    public void Consumer_integration_slot_is_optional_and_preserved()
    {
        var integration = ConsumerIntegration.Empty;
        var options = D3D12OpusApplicationOptions.Default with { ConsumerIntegration = integration };

        options.ConsumerIntegration.Should().BeSameAs(integration);
    }

    [Theory]
    [InlineData("", D3D12OpusApplicationOptions.DefaultWindowWidth, D3D12OpusApplicationOptions.DefaultWindowHeight)]
    [InlineData(" ", D3D12OpusApplicationOptions.DefaultWindowWidth, D3D12OpusApplicationOptions.DefaultWindowHeight)]
    public void Empty_window_title_is_rejected(string title, int width, int height)
    {
        var options = D3D12OpusApplicationOptions.Default with { WindowTitle = title, WindowWidth = width, WindowHeight = height };

        var act = () => _ = TryBuild(options);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(159, 200)]
    [InlineData(200, 119)]
    public void Window_below_minimum_dimensions_is_rejected(int width, int height)
    {
        var options = D3D12OpusApplicationOptions.Default with { WindowWidth = width, WindowHeight = height };

        var act = () => _ = TryBuild(options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Metrics_window_below_one_is_rejected()
    {
        var options = D3D12OpusApplicationOptions.Default with { MetricsWindow = 0 };

        var act = () => _ = TryBuild(options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Empty_diagnostics_directory_is_rejected()
    {
        var options = D3D12OpusApplicationOptions.Default with { DiagnosticsDirectory = " " };

        var act = () => _ = TryBuild(options);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Invalid_overlay_options_are_rejected()
    {
        var overlay = DiagnosticOverlayOptions.Default with { MaxRows = 0 };
        var options = D3D12OpusApplicationOptions.Default with { DiagnosticOverlay = overlay };

        var act = () => _ = TryBuild(options);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static D3D12OpusHostInstance? TryBuild(D3D12OpusApplicationOptions options) =>
        new D3D12OpusHostBuilder().WithOptions(options).TryBuild();
}
