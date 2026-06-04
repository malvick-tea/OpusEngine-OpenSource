using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Versioning;

public sealed class AppVersionTests
{
    [Fact]
    public void Dev_sentinel_round_trips()
    {
        AppVersion.Dev.IsDev.Should().BeTrue();
        AppVersion.Dev.ToString().Should().Be("0.0.0-dev+local");
    }

    [Theory]
    [InlineData("1.2.3", 1, 2, 3, "", "")]
    [InlineData("1.2.3-alpha", 1, 2, 3, "alpha", "")]
    [InlineData("1.2.3-rc.1+sha.abcdef", 1, 2, 3, "rc.1", "sha.abcdef")]
    [InlineData("0.0.0-dev+local", 0, 0, 0, "dev", "local")]
    public void Parses_semver(string text, int major, int minor, int patch, string pre, string build)
    {
        var v = AppVersion.Parse(text);
        v.Major.Should().Be(major);
        v.Minor.Should().Be(minor);
        v.Patch.Should().Be(patch);
        v.PreRelease.Should().Be(pre);
        v.Build.Should().Be(build);
    }

    [Fact]
    public void Empty_returns_dev()
    {
        AppVersion.Parse(string.Empty).Should().Be(AppVersion.Dev);
    }
}
