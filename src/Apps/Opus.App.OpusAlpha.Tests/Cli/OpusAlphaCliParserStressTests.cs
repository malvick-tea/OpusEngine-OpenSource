using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserStressTests
{
    [Fact]
    public void Stress_default_iteration_count_matches_documented_constant()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress" });

        args.Mode.Should().Be(OpusAlphaMode.Stress);
        args.StressIterations.Should().Be(OpusAlphaArgs.DefaultStressIterations);
    }

    [Fact]
    public void Stress_iterations_option_overrides_default()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--iterations", "12" });

        args.StressIterations.Should().Be(12);
    }

    [Fact]
    public void Stress_iterations_rejects_zero()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--iterations", "0" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--iterations");
    }

    [Fact]
    public void Stress_iterations_rejects_unparseable_value()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--iterations", "not-a-number" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("integer");
    }

    [Fact]
    public void Stress_iterations_rejects_missing_value()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--iterations" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("missing");
    }

    [Fact]
    public void Stress_dir_option_routes_to_stress_report_directory()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--stress-dir", "C:/diagnostics/stress" });

        args.StressReportDirectory.Should().Be("C:/diagnostics/stress");
    }

    [Fact]
    public void Stress_known_issues_option_routes_to_known_issues_path()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--known-issues", "C:/known-issues.json" });

        args.KnownIssuesPath.Should().Be("C:/known-issues.json");
    }

    [Fact]
    public void Stress_combines_frame_count_with_iteration_count()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--iterations", "3", "--frames", "30" });

        args.StressIterations.Should().Be(3);
        args.SmokeFrameCount.Should().Be(30);
    }

    [Fact]
    public void Stress_combines_scene_scale()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--scene", "large", "--iterations", "2" });

        args.SceneScale.Should().Be(Opus.Engine.AlphaHarness.Scenes.AlphaSceneScale.Large);
        args.StressIterations.Should().Be(2);
    }

    [Fact]
    public void Stress_default_does_not_enable_network_injection()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress" });

        args.EnableStressNetworkInjection.Should().BeFalse();
        args.StressInjectionLossRate.Should().Be(0.0);
        args.StressInjectionLatencyMilliseconds.Should().Be(0);
    }

    [Fact]
    public void Stress_inject_loss_enables_network_injection()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-loss", "0.25" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionLossRate.Should().BeApproximately(0.25, 1e-9);
    }

    [Fact]
    public void Stress_inject_latency_routes_to_latency_milliseconds()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-latency-ms", "12" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionLatencyMilliseconds.Should().Be(12);
    }

    [Fact]
    public void Stress_inject_seed_routes_to_injection_seed()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-seed", "42" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionSeed.Should().Be(42);
    }

    [Fact]
    public void Stress_inject_peers_routes_to_injection_peer_cohort()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-peers", "6" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionPeers.Should().Be(6);
    }

    [Fact]
    public void Stress_inject_packets_routes_to_injection_packets_per_peer()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-packets", "10" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionPacketsPerPeer.Should().Be(10);
    }

    [Fact]
    public void Stress_inject_payload_routes_to_injection_payload_bytes()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-payload", "128" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionPayloadBytes.Should().Be(128);
    }

    [Fact]
    public void Stress_inject_drop_tolerance_routes_to_tolerance()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-drop-tolerance", "0.4" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionDropTolerance.Should().BeApproximately(0.4, 1e-9);
    }

    [Fact]
    public void Stress_inject_loss_rejects_out_of_range()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-loss", "1.5" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--inject-loss");
    }

    [Fact]
    public void Stress_inject_loss_rejects_unparseable_value()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-loss", "abc" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--inject-loss");
    }

    [Fact]
    public void Stress_inject_drop_tolerance_rejects_negative()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-drop-tolerance", "-0.1" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--inject-drop-tolerance");
    }

    [Fact]
    public void Stress_inject_latency_rejects_negative_milliseconds()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-latency-ms", "-1" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--inject-latency-ms");
    }

    [Fact]
    public void Stress_inject_options_combine_into_one_args_record()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "stress",
            "--inject-loss", "0.10",
            "--inject-latency-ms", "5",
            "--inject-peers", "3",
        });

        args.Mode.Should().Be(OpusAlphaMode.Stress);
        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionLossRate.Should().BeApproximately(0.10, 1e-9);
        args.StressInjectionLatencyMilliseconds.Should().Be(5);
        args.StressInjectionPeers.Should().Be(3);
    }

    [Fact]
    public void Stress_inject_inbound_loss_enables_injection_and_routes_value()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-inbound-loss", "0.4" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionInboundLossRate.Should().BeApproximately(0.4, 1e-9);
    }

    [Fact]
    public void Stress_inject_inbound_latency_routes_to_inbound_milliseconds()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-inbound-latency-ms", "20" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionInboundLatencyMilliseconds.Should().Be(20);
    }

    [Fact]
    public void Stress_inject_inbound_seed_routes_to_inbound_seed()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-inbound-seed", "999" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionInboundSeed.Should().Be(999);
    }

    [Fact]
    public void Stress_inject_inbound_drop_tolerance_routes_to_inbound_tolerance()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-inbound-drop-tolerance", "0.3" });

        args.EnableStressNetworkInjection.Should().BeTrue();
        args.StressInjectionInboundDropTolerance.Should().BeApproximately(0.3, 1e-9);
    }

    [Fact]
    public void Stress_inject_inbound_loss_rejects_out_of_range()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-inbound-loss", "1.5" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--inject-inbound-loss");
    }

    [Fact]
    public void Stress_inject_inbound_latency_rejects_negative()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress", "--inject-inbound-latency-ms", "-5" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--inject-inbound-latency-ms");
    }

    [Fact]
    public void Stress_default_inbound_tolerance_disabled()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "stress" });

        args.StressInjectionInboundDropTolerance.Should().Be(OpusAlphaArgs.DefaultStressInjectionInboundDropTolerance);
        args.StressInjectionInboundDropTolerance.Should().Be(1.0);
    }
}
