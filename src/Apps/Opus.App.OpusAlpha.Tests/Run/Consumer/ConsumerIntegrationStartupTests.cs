using System;
using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Run.Consumer;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run.Consumer;

public sealed class ConsumerIntegrationStartupTests
{
    private const string FixtureAssemblyFileName = "Opus.App.OpusAlpha.Tests.ConsumerPluginFixture.dll";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_proceeds_without_an_integration_when_no_path_is_supplied(string? path)
    {
        var resolution = ConsumerIntegrationStartup.Resolve(path);

        resolution.CanProceed.Should().BeTrue();
        resolution.Integration.Should().BeNull();
        resolution.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Resolve_aborts_when_a_requested_consumer_cannot_be_loaded()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"opus-missing-consumer-{Guid.NewGuid():N}.dll");

        var resolution = ConsumerIntegrationStartup.Resolve(missing);

        resolution.CanProceed.Should().BeFalse();
        resolution.Integration.Should().BeNull();
        resolution.FailureReason.Should().Contain("was not found");
    }

    [Fact]
    public void Resolve_loads_the_integration_from_the_fixture_plugin_assembly()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, FixtureAssemblyFileName);
        File.Exists(fixturePath).Should().BeTrue();

        var resolution = ConsumerIntegrationStartup.Resolve(fixturePath);

        resolution.CanProceed.Should().BeTrue();
        resolution.Integration.Should().NotBeNull();
        resolution.Integration!.HasContracts.Should().BeTrue();
        resolution.FailureReason.Should().BeNull();
    }
}
