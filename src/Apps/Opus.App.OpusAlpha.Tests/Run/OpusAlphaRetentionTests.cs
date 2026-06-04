using FluentAssertions;
using Opus.App.OpusAlpha.Run;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class OpusAlphaRetentionTests
{
    [Fact]
    public void Artifacts_policy_is_active_and_matches_the_diagnostics_default()
    {
        OpusAlphaRetention.Artifacts.IsActive.Should().BeTrue();
        OpusAlphaRetention.Artifacts.Should().Be(DiagnosticsArtifactRetentionPolicy.Default);
        OpusAlphaRetention.Artifacts.MaxPairCount
            .Should().Be(DiagnosticsArtifactRetentionPolicy.DefaultMaxPairCount);
    }

    [Fact]
    public void Logs_policy_is_active_and_matches_the_rolling_log_default()
    {
        OpusAlphaRetention.Logs.IsActive.Should().BeTrue();
        OpusAlphaRetention.Logs.Should().Be(RollingLogRetentionPolicy.Default);
        OpusAlphaRetention.Logs.MaxFileCount.Should().Be(RollingLogRetentionPolicy.DefaultMaxFileCount);
    }
}
