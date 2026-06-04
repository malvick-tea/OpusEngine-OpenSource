using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Versioning;

public sealed class EngineIdentityTests
{
    [Fact]
    public void Current_exposes_opus_01_alpha_identity()
    {
        var identity = EngineIdentity.Current;

        identity.ProductName.Should().Be("Opus");
        identity.DisplayVersion.Should().Be("0.1");
        identity.DisplayName.Should().Be("Opus 0.1");
        identity.ReleaseChannel.Should().Be("alpha");
        identity.ProductVersion.Should().Be(new AppVersion(0, 1, 0, "alpha", string.Empty));
        identity.ToIdentityLine().Should().Be("Opus 0.1 (0.1.0-alpha)");
    }

    [Fact]
    public void Current_exposes_opus_assembly_prefix()
    {
        var identity = EngineIdentity.Current;

        identity.AssemblyNamePrefix.Should().Be("Opus.*");
        identity.AssemblyCompatibility.Should().Contain("Opus");
        identity.AssemblyCompatibility.Should().Contain("normalised");
    }
}
